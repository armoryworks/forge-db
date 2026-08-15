using System.Text;
using Npgsql;

namespace Forge.Db;

/// <summary>
/// Loads a <see cref="DataDumper"/> dump into a database whose schema is already provisioned (a
/// fresh <c>apply</c> target) — the second half of the clean-rebuild workflow (docs/DESIGN §6.2).
/// Garbage is cleaned out at three points, in order:
///
/// <list type="number">
/// <item><b>--exclude globs</b> — tables that simply don't come along (event logs, dead features).</item>
/// <item><b><c>scrub/</c> scripts</b> — version-controlled cleanup SQL run after the load, every
/// import (NOT applied-once): purge soft-deleted rows, orphaned attachments, expired tokens.</item>
/// <item><b>FK orphan validation</b> — the load runs with FK triggers suspended
/// (<c>session_replication_role = replica</c>), so a final pass re-checks every foreign key and
/// reports rows whose parents didn't make the trip. Orphans fail the import (exit 4) unless
/// explicitly allowed — they are precisely the garbage this workflow exists to surface.</item>
/// </list>
///
/// <para>The load itself is one transaction: <c>TRUNCATE … RESTART IDENTITY CASCADE</c> over every
/// selected table, then <c>COPY … FROM STDIN</c> per table using the intersection of dumped and
/// current columns (dropped columns fall away; new columns take their defaults). A failure rolls the
/// whole load back. Afterwards: sequences are bumped past <c>max(id)</c>, scrub runs, FKs are
/// validated, and <c>ANALYZE</c> refreshes stats.</para>
/// </summary>
public static class DataImporter
{
    public sealed record TableResult(string Qualified, long Rows, IReadOnlyList<string> DroppedColumns);

    public sealed record OrphanViolation(string Constraint, string ChildTable, string ParentTable, long Rows);

    public sealed record Result(
        IReadOnlyList<TableResult> Loaded,
        IReadOnlyList<string> Excluded,
        IReadOnlyList<string> MissingInTarget,
        IReadOnlyList<string> ScrubScripts,
        IReadOnlyList<OrphanViolation> Orphans);

    public static Result Run(
        string repoRoot, string dbUrl, string dumpDir,
        IReadOnlyList<string> excludePatterns, bool skipScrub)
    {
        var manifest = DumpManifest.Load(dumpDir);
        WarnOnFingerprintSkew(repoRoot, manifest);

        var connString = DbUrl.ToNpgsql(dbUrl);
        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        var targetCols = TargetColumns(conn);

        // ── Select: manifest minus excludes minus tables the target schema no longer has ──────────
        var excluded = new List<string>();
        var missing = new List<string>();
        var selected = new List<(DumpManifest.Table Entry, IReadOnlyList<string> Cols, IReadOnlyList<string> Dropped)>();
        foreach (var t in manifest.Tables)
        {
            if (TableGlob.MatchesAny(excludePatterns, t.Schema, t.Name)) { excluded.Add(t.Qualified); continue; }
            if (!targetCols.TryGetValue(t.Qualified, out var current)) { missing.Add(t.Qualified); continue; }
            var cols = t.Columns.Where(current.Contains).ToList();
            var dropped = t.Columns.Where(c => !current.Contains(c)).ToList();
            if (cols.Count == 0) { missing.Add(t.Qualified); continue; }
            selected.Add((t, cols, dropped));
        }
        foreach (var m in missing)
            Console.Error.WriteLine($"[import] WARNING: {m} is in the dump but not the target schema — skipped.");

        // FK triggers off for the load: dump order is arbitrary, and excluded tables may be FK
        // parents. Orphans this creates are re-checked (and reported) below.
        using (var cmd = new NpgsqlCommand("SET session_replication_role = replica", conn))
            TryElevate(cmd);

        // ── One transaction: truncate everything selected, then stream every COPY ─────────────────
        var loaded = new List<TableResult>();
        using (var tx = conn.BeginTransaction())
        {
            var truncateList = string.Join(", ", selected.Select(s => DataDumper.Qualify(s.Entry.Schema, s.Entry.Name)));
            if (truncateList.Length > 0)
                using (var cmd = new NpgsqlCommand($"TRUNCATE {truncateList} RESTART IDENTITY CASCADE", conn, tx))
                    cmd.ExecuteNonQuery();

            foreach (var (entry, cols, dropped) in selected)
            {
                var colList = string.Join(", ", cols.Select(DataDumper.QuoteIdent));
                var qualified = DataDumper.Qualify(entry.Schema, entry.Name);
                using (var writer = conn.BeginTextImport($"COPY {qualified} ({colList}) FROM STDIN"))
                using (var reader = OpenDumpFile(dumpDir, entry, cols, dropped))
                {
                    var buf = new char[64 * 1024];
                    int n;
                    while ((n = reader.Read(buf, 0, buf.Length)) > 0)
                        writer.Write(buf, 0, n);
                }
                loaded.Add(new TableResult(entry.Qualified, entry.Rows, dropped));
                Console.WriteLine($"[import]  {entry.Qualified,-60} {entry.Rows,10:n0} rows"
                                  + (dropped.Count > 0 ? $"  (dropped: {string.Join(", ", dropped)})" : ""));
            }
            tx.Commit();
        }

        using (var cmd = new NpgsqlCommand("SET session_replication_role = origin", conn))
            cmd.ExecuteNonQuery();

        FixSequences(conn, selected.Select(s => (s.Entry.Schema, s.Entry.Name, s.Cols)).ToList());

        var scrubbed = skipScrub ? [] : RunScrub(repoRoot, conn);

        var orphans = FindFkOrphans(conn, selected.Select(s => s.Entry.Qualified).ToHashSet(StringComparer.Ordinal));

        using (var cmd = new NpgsqlCommand("ANALYZE", conn)) { cmd.CommandTimeout = 600; cmd.ExecuteNonQuery(); }

        return new Result(loaded, excluded, missing, scrubbed, orphans);
    }

    private static void WarnOnFingerprintSkew(string repoRoot, DumpManifest manifest)
    {
        var current = DataDumper.SchemaFingerprint(repoRoot);
        if (manifest.SchemaFingerprint is not null && current is not null && manifest.SchemaFingerprint != current)
            Console.Error.WriteLine(
                "[import] WARNING: schema/ has changed since this dump was taken — loading the column " +
                "intersection per table (dropped columns fall away; new columns take their defaults).");
    }

    private static void TryElevate(NpgsqlCommand cmd)
    {
        try { cmd.ExecuteNonQuery(); }
        catch (PostgresException ex) when (ex.SqlState == "42501")
        {
            throw new InvalidOperationException(
                "import needs to suspend FK triggers for the load (SET session_replication_role = replica), " +
                "which requires a superuser connection. Point --db at a superuser (the self-host stack's " +
                "postgres user qualifies).", ex);
        }
    }

    /// <summary>All COPY-able columns per target table, keyed by <c>schema.table</c>.</summary>
    private static Dictionary<string, HashSet<string>> TargetColumns(NpgsqlConnection conn)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var t in DataDumper.DiscoverTables(conn))
            map[$"{t.Schema}.{t.Name}"] = t.Columns.ToHashSet(StringComparer.Ordinal);
        return map;
    }

    /// <summary>
    /// Opens the table's COPY text stream. When no columns were dropped this is the raw file; when
    /// the target lost columns since the dump, rows are re-projected onto the surviving columns
    /// (COPY text is tab-separated with in-value tabs escaped as <c>\t</c>, so a raw split on
    /// unescaped tabs is exact).
    /// </summary>
    private static TextReader OpenDumpFile(
        string dumpDir, DumpManifest.Table entry, IReadOnlyList<string> cols, IReadOnlyList<string> dropped)
    {
        var path = Path.Combine(dumpDir, entry.File);
        if (!File.Exists(path))
            throw new FileNotFoundException($"dump file listed in the manifest is missing: {entry.File}");
        var raw = new StreamReader(path, Encoding.UTF8);
        if (dropped.Count == 0) return raw;

        var keep = entry.Columns.Select((c, i) => (c, i)).Where(x => cols.Contains(x.c)).Select(x => x.i).ToArray();
        return new ProjectingReader(raw, entry.Columns.Count, keep);
    }

    /// <summary>Line-by-line projection of COPY text rows onto a subset of source columns.</summary>
    private sealed class ProjectingReader(StreamReader inner, int sourceCols, int[] keep) : TextReader
    {
        private readonly StringBuilder _pending = new();

        public override int Read(char[] buffer, int index, int count)
        {
            while (_pending.Length < count)
            {
                var line = inner.ReadLine();
                if (line is null) break;
                _pending.Append(Project(line)).Append('\n');
            }
            var n = Math.Min(count, _pending.Length);
            _pending.CopyTo(0, buffer, index, n);
            _pending.Remove(0, n);
            return n;
        }

        private string Project(string line)
        {
            var fields = SplitCopyLine(line, sourceCols);
            return string.Join('\t', keep.Select(i => fields[i]));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Split one COPY text row on unescaped tabs (in-value tabs arrive as the two-character
    /// escape <c>\t</c>, never a literal tab, so this is exact).</summary>
    public static string[] SplitCopyLine(string line, int expectedFields)
    {
        var fields = new string[expectedFields];
        var idx = 0;
        var start = 0;
        for (var i = 0; i < line.Length && idx < expectedFields - 1; i++)
        {
            if (line[i] == '\t')
            {
                fields[idx++] = line[start..i];
                start = i + 1;
            }
        }
        fields[idx] = line[start..];
        return fields;
    }

    /// <summary>Bump every serial/identity sequence past the loaded data — TRUNCATE reset them, and
    /// COPY does not advance them.</summary>
    private static void FixSequences(
        NpgsqlConnection conn, IReadOnlyList<(string Schema, string Name, IReadOnlyList<string> Cols)> tables)
    {
        foreach (var (schema, name, cols) in tables)
        {
            var qualified = DataDumper.Qualify(schema, name);
            foreach (var col in cols)
            {
                string? seq;
                using (var find = new NpgsqlCommand("SELECT pg_get_serial_sequence(@tbl, @col)", conn))
                {
                    find.Parameters.AddWithValue("tbl", qualified);
                    find.Parameters.AddWithValue("col", col);
                    seq = find.ExecuteScalar() as string;
                }
                if (seq is null) continue;

                // Identifiers come straight from the catalog and are quote-escaped — not user input.
                using var set = new NpgsqlCommand(
                    $"SELECT setval(@seq, GREATEST(COALESCE(max({DataDumper.QuoteIdent(col)}), 0), 1), " +
                    $"max({DataDumper.QuoteIdent(col)}) IS NOT NULL) FROM {qualified}", conn);
                set.Parameters.AddWithValue("seq", seq);
                set.ExecuteScalar();
            }
        }
    }

    /// <summary>Run every <c>scrub/*.sql</c> in filename order, each in its own transaction. Unlike
    /// <c>data/</c>+<c>seed/</c> these are NOT applied-once: every import runs the full set, so they
    /// must be authored idempotent (plain DELETE/UPDATE with WHERE guards is naturally so).</summary>
    private static IReadOnlyList<string> RunScrub(string repoRoot, NpgsqlConnection conn)
    {
        var dir = Path.Combine(repoRoot, SchemaLayout.ScrubDir);
        if (!Directory.Exists(dir)) return [];
        var run = new List<string>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.sql", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = $"{SchemaLayout.ScrubDir}/{Path.GetFileName(f)}";
            using var tx = conn.BeginTransaction();
            using (var cmd = new NpgsqlCommand(File.ReadAllText(f), conn, tx))
            {
                cmd.CommandTimeout = 600;
                var affected = cmd.ExecuteNonQuery();
                Console.WriteLine($"[import]  scrub {name} ({(affected < 0 ? "ok" : $"{affected} rows")})");
            }
            tx.Commit();
            run.Add(name);
        }
        return run;
    }

    /// <summary>
    /// Re-check every FK involving a loaded table: the load ran with FK triggers suspended, so
    /// nothing enforced them. Each violation is child rows whose parent didn't make the trip —
    /// either add a scrub rule for them or re-include the excluded parent.
    /// </summary>
    public static IReadOnlyList<OrphanViolation> FindFkOrphans(NpgsqlConnection conn, ISet<string> loadedTables)
    {
        const string fkSql = """
            SELECT con.conname,
                   cn.nspname, cc.relname,
                   pn.nspname, pc.relname,
                   ARRAY(SELECT a.attname FROM unnest(con.conkey) WITH ORDINALITY k(attnum, ord)
                         JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
                         ORDER BY k.ord),
                   ARRAY(SELECT a.attname FROM unnest(con.confkey) WITH ORDINALITY k(attnum, ord)
                         JOIN pg_attribute a ON a.attrelid = con.confrelid AND a.attnum = k.attnum
                         ORDER BY k.ord)
            FROM pg_constraint con
            JOIN pg_class cc ON cc.oid = con.conrelid
            JOIN pg_namespace cn ON cn.oid = cc.relnamespace
            JOIN pg_class pc ON pc.oid = con.confrelid
            JOIN pg_namespace pn ON pn.oid = pc.relnamespace
            WHERE con.contype = 'f'
            """;
        var fks = new List<(string Name, string CSchema, string CTable, string PSchema, string PTable, string[] CCols, string[] PCols)>();
        using (var cmd = new NpgsqlCommand(fkSql, conn))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                fks.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.GetFieldValue<string[]>(5), r.GetFieldValue<string[]>(6)));

        var violations = new List<OrphanViolation>();
        foreach (var fk in fks)
        {
            if (!loadedTables.Contains($"{fk.CSchema}.{fk.CTable}")) continue;

            var child = DataDumper.Qualify(fk.CSchema, fk.CTable);
            var parent = DataDumper.Qualify(fk.PSchema, fk.PTable);
            var notNull = string.Join(" AND ", fk.CCols.Select(c => $"c.{DataDumper.QuoteIdent(c)} IS NOT NULL"));
            var join = string.Join(" AND ", fk.CCols.Zip(fk.PCols,
                (cc, pc) => $"p.{DataDumper.QuoteIdent(pc)} = c.{DataDumper.QuoteIdent(cc)}"));
            using var cmd = new NpgsqlCommand(
                $"SELECT count(*) FROM {child} c WHERE {notNull} AND NOT EXISTS (SELECT 1 FROM {parent} p WHERE {join})",
                conn);
            cmd.CommandTimeout = 600;
            var count = (long)cmd.ExecuteScalar()!;
            if (count > 0)
                violations.Add(new OrphanViolation(fk.Name, $"{fk.CSchema}.{fk.CTable}", $"{fk.PSchema}.{fk.PTable}", count));
        }
        return violations;
    }
}
