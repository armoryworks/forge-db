using System.Text;
using System.Text.RegularExpressions;

namespace Forge.Db;

/// <summary>
/// Splits a <c>pg_dump --schema-only --no-owner --no-privileges</c> file into the desired-state
/// <c>schema/</c> tree (docs/DESIGN.md §3, §4 <c>baseline</c> verb). Deterministic and pure: the
/// same dump always yields the same tree. EF's <c>__EFMigrationsHistory</c> bookkeeping table and
/// its child objects are dropped — forge-db does not own them.
///
/// Routing by pg_dump object Type:
///   EXTENSION                                  -> schema/extensions/&lt;name&gt;.sql
///   FUNCTION                                   -> schema/functions/&lt;name&gt;.sql
///   TABLE                                      -> schema/tables/&lt;table&gt;.sql (CREATE TABLE)
///   SEQUENCE | DEFAULT | CONSTRAINT | FK …     -> appended to schema/tables/&lt;owning table&gt;.sql
///   INDEX                                      -> schema/indexes/&lt;index&gt;.sql
///   TRIGGER                                    -> schema/triggers/&lt;trigger&gt;.sql
///   COMMENT                                    -> appended to the commented object's file
/// Within a table file the order is: CREATE TABLE, identity (sequence/default), PK/UQ/CK, FK —
/// a readable, self-contained table definition (the diff engine derives apply order, not us).
/// </summary>
public static class SqlDumpSplitter
{
    private static readonly Regex HeaderRe = new(
        @"^--\r?\n-- Name: (?<name>.*?); Type: (?<type>[A-Z ]+); Schema: (?<schema>.*?); Owner:.*?\r?\n--\r?\n",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // ALTER TABLE [ONLY] [public.]"?table"? — owning table for constraints/sequences/defaults.
    private static readonly Regex AlterTableRe = new(
        @"ALTER TABLE (?:ONLY )?(?:public\.)?""?(?<t>[A-Za-z_][A-Za-z0-9_]*)""?",
        RegexOptions.Compiled);

    // ON [public.]"?table"? — owning table for indexes and triggers.
    private static readonly Regex OnTableRe = new(
        @"\bON (?:public\.)?""?(?<t>[A-Za-z_][A-Za-z0-9_]*)""?",
        RegexOptions.Compiled);

    /// <summary>Parse a dump into its constituent objects (preamble SETs / psql meta dropped).</summary>
    public static IReadOnlyList<SqlDumpObject> Parse(string dump)
    {
        var objects = new List<SqlDumpObject>();
        var matches = HeaderRe.Matches(dump);
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var bodyStart = m.Index + m.Length;
            var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : dump.Length;
            var sql = CleanBody(dump[bodyStart..bodyEnd]);
            if (sql.Length == 0) continue;
            objects.Add(new SqlDumpObject(
                m.Groups["name"].Value.Trim(),
                m.Groups["type"].Value.Trim(),
                sql));
        }
        return objects;
    }

    /// <summary>Drop pg_dump structural noise, keep the object's real SQL (incl. inline comments).</summary>
    private static string CleanBody(string raw)
    {
        var keep = new List<string>();
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.TrimEnd();
            if (t.StartsWith('\\')) continue;                       // psql meta: \restrict \unrestrict \connect
            if (t.StartsWith("-- PostgreSQL database dump")) continue;
            if (t.StartsWith("-- Completed on") || t.StartsWith("-- Dumped")) continue;
            keep.Add(t);
        }
        return string.Join('\n', keep).Trim();
    }

    /// <summary>Write the full tree under <paramref name="repoRoot"/>. Returns per-dir file counts.</summary>
    public static SplitResult Write(string dump, string repoRoot)
    {
        var objects = Parse(dump);

        // Per-table buckets so a table file is assembled in a stable, readable order.
        var tableCreate = new Dictionary<string, string>(StringComparer.Ordinal);
        var tableIdentity = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var tableChecks = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // PK/UQ/CK
        var tableFks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var tableComments = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var standalone = new List<(string Dir, string FileName, string Sql)>(); // extensions/functions/indexes/triggers
        var skippedEf = 0;

        void AddTo(Dictionary<string, List<string>> map, string table, string sql)
        {
            if (!map.TryGetValue(table, out var list)) map[table] = list = new List<string>();
            list.Add(sql);
        }

        foreach (var o in objects)
        {
            switch (o.Type)
            {
                case "EXTENSION":
                    standalone.Add((SchemaLayout.Extensions, Sanitize(o.Name), o.Sql));
                    break;

                case "FUNCTION":
                    standalone.Add((SchemaLayout.Functions, Sanitize(StripSignature(o.Name)), o.Sql));
                    break;

                case "TABLE":
                {
                    var table = o.Name;
                    if (IsEf(table)) { skippedEf++; break; }
                    tableCreate[table] = o.Sql;
                    break;
                }

                case "SEQUENCE":
                case "DEFAULT":
                {
                    var table = OwningTable(o, fromAlter: true);
                    if (table is null || IsEf(table)) { if (table is not null && IsEf(table)) skippedEf++; break; }
                    AddTo(tableIdentity, table, o.Sql);
                    break;
                }

                case "CONSTRAINT":
                {
                    var table = FirstToken(o.Name) ?? OwningTable(o, fromAlter: true);
                    if (table is null || IsEf(table)) { skippedEf++; break; }
                    AddTo(tableChecks, table, o.Sql);
                    break;
                }

                case "FK CONSTRAINT":
                {
                    var table = FirstToken(o.Name) ?? OwningTable(o, fromAlter: true);
                    if (table is null || IsEf(table)) { skippedEf++; break; }
                    AddTo(tableFks, table, o.Sql);
                    break;
                }

                case "INDEX":
                {
                    var idx = SecondToken(o.Name) ?? o.Name;
                    var table = OwningTable(o, fromAlter: false);
                    if (table is not null && IsEf(table)) { skippedEf++; break; }
                    standalone.Add((SchemaLayout.Indexes, Sanitize(idx), o.Sql));
                    break;
                }

                case "TRIGGER":
                {
                    var trg = SecondToken(o.Name) ?? o.Name;
                    standalone.Add((SchemaLayout.Triggers, Sanitize(trg), o.Sql));
                    break;
                }

                case "COMMENT":
                    RouteComment(o, tableComments, standalone);
                    break;

                default:
                    // VIEW, MATERIALIZED VIEW, TYPE, etc. — none in the current baseline, but
                    // route unknown-but-named objects to a misc file rather than silently dropping.
                    standalone.Add((SchemaLayout.SchemaDir, "_unrouted_" + Sanitize(o.Type.ToLowerInvariant()), o.Sql));
                    break;
            }
        }

        // Reset the tree's object dirs so re-running baseline is idempotent (history/ etc. untouched).
        foreach (var sub in SchemaLayout.AllSubDirs)
        {
            var dir = Path.Combine(repoRoot, sub);
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*.sql")) File.Delete(f);
            Directory.CreateDirectory(dir);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Bump(string dir) => counts[dir] = counts.GetValueOrDefault(dir) + 1;

        // Assemble + write table files.
        foreach (var (table, create) in tableCreate.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var sb = new StringBuilder();
            sb.Append(create.TrimEnd()).Append('\n');
            AppendSection(sb, tableIdentity, table);
            AppendSection(sb, tableChecks, table);
            AppendSection(sb, tableFks, table);
            if (tableComments.TryGetValue(table, out var cmts))
                foreach (var c in cmts) sb.Append('\n').Append(c.TrimEnd()).Append('\n');
            WriteFile(Path.Combine(repoRoot, SchemaLayout.Tables, Sanitize(table) + ".sql"), sb.ToString());
            Bump(SchemaLayout.Tables);
        }

        // Write standalone objects (extensions / functions / indexes / triggers / misc). Multiple
        // objects can target one file (e.g. CREATE EXTENSION + its COMMENT) — concatenate, never
        // overwrite. GroupBy preserves first-seen order, so create precedes its comment.
        foreach (var grp in standalone.GroupBy(s => (s.Dir, s.FileName)))
        {
            var sql = string.Join("\n\n", grp.Select(s => s.Sql.TrimEnd()));
            WriteFile(Path.Combine(repoRoot, grp.Key.Dir, grp.Key.FileName + ".sql"), sql + "\n");
            Bump(grp.Key.Dir);
        }

        return new SplitResult(objects.Count, skippedEf, counts);
    }

    private static void AppendSection(StringBuilder sb, Dictionary<string, List<string>> map, string table)
    {
        if (!map.TryGetValue(table, out var list)) return;
        foreach (var sql in list) sb.Append('\n').Append(sql.TrimEnd()).Append('\n');
    }

    private static void RouteComment(
        SqlDumpObject o,
        Dictionary<string, List<string>> tableComments,
        List<(string, string, string)> standalone)
    {
        // Name looks like "EXTENSION vector" / "TABLE foo" / "COLUMN foo.bar".
        var parts = o.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var kind = parts[0];
            var target = parts[1];
            if (kind is "TABLE" or "COLUMN")
            {
                var table = target.Split('.', '(')[0];
                if (!IsEf(table))
                {
                    if (!tableComments.TryGetValue(table, out var l)) tableComments[table] = l = new();
                    l.Add(o.Sql);
                }
                return;
            }
            if (kind == "EXTENSION")
            {
                standalone.Add((SchemaLayout.Extensions, Sanitize(target), o.Sql));
                return;
            }
        }
        standalone.Add((SchemaLayout.SchemaDir, "_comments", o.Sql));
    }

    private static bool IsEf(string table) =>
        table.Equals(SchemaLayout.EfHistoryTable, StringComparison.Ordinal) ||
        table.StartsWith(SchemaLayout.EfHistoryTable, StringComparison.Ordinal);

    private static string? FirstToken(string name)
    {
        var i = name.IndexOf(' ');
        return i > 0 ? name[..i] : null;
    }

    private static string? SecondToken(string name)
    {
        var i = name.IndexOf(' ');
        return i > 0 && i + 1 < name.Length ? name[(i + 1)..].Trim() : null;
    }

    private static string? OwningTable(SqlDumpObject o, bool fromAlter)
    {
        var m = (fromAlter ? AlterTableRe : OnTableRe).Match(o.Sql);
        return m.Success ? m.Groups["t"].Value : null;
    }

    private static string StripSignature(string funcName)
    {
        var i = funcName.IndexOf('(');
        return i > 0 ? funcName[..i] : funcName;
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        return sb.ToString();
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}

/// <summary>Outcome of a split: total objects parsed, EF objects skipped, files written per dir.</summary>
public sealed record SplitResult(int ObjectsParsed, int EfObjectsSkipped, IReadOnlyDictionary<string, int> FilesPerDir);
