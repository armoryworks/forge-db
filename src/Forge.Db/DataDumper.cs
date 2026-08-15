using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Forge.Db;

/// <summary>
/// Streams every application table's data out of a live database into a dump directory — one
/// <c>COPY … TO STDOUT</c> text file per table under <c>tables/</c>, plus a <see cref="DumpManifest"/>.
/// Read-only against the source. Pairs with <see cref="DataImporter"/>, whose job is to load a dump
/// into a <b>freshly provisioned</b> database (the clean-rebuild workflow, docs/DESIGN §6.2).
///
/// <para>Excluded, mirroring the pg-schema-diff reconcile: the <c>hangfire</c> and <c>forge_db</c>
/// schemas (infrastructure that re-creates itself) and EF's <c>__EFMigrationsHistory</c>. Generated
/// columns are omitted from the column list — they cannot be COPYed back in.</para>
///
/// <para>COPY <b>text</b> format deliberately: it is stable across Postgres versions, diffable when
/// something needs a forensic look, and — because the manifest records the column list — the import
/// side can load the intersection of dumped and current columns, tolerating modest schema drift
/// where the binary format would hard-fail.</para>
/// </summary>
public static class DataDumper
{
    /// <summary>Schemas never dumped (infrastructure that provisions itself).</summary>
    public static readonly string[] ExcludedSchemas = ["pg_catalog", "information_schema", "pg_toast", "hangfire", "forge_db"];

    public sealed record TableRef(string Schema, string Name, IReadOnlyList<string> Columns);

    public static DumpManifest Run(string repoRoot, string dbUrl, string outDir)
    {
        var connString = DbUrl.ToNpgsql(dbUrl);
        Directory.CreateDirectory(Path.Combine(outDir, "tables"));

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        var tables = DiscoverTables(conn);
        var entries = new List<DumpManifest.Table>();
        foreach (var t in tables)
        {
            var entry = DumpTable(conn, t, outDir);
            entries.Add(entry);
            Console.WriteLine($"[dump]   {entry.Qualified,-60} {entry.Rows,10:n0} rows");
        }

        var manifest = new DumpManifest(
            DumpedAtUtc: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            SourceHost: conn.Host ?? "?",
            SourceDatabase: conn.Database ?? "?",
            SchemaFingerprint: SchemaFingerprint(repoRoot),
            Tables: entries);
        manifest.Save(outDir);
        return manifest;
    }

    /// <summary>SHA-256 of the assembled desired state, so import can flag dump/schema skew. Null when
    /// run outside a forge-db repo (the dump is still usable — the check just can't run).</summary>
    public static string? SchemaFingerprint(string repoRoot)
    {
        if (!Directory.Exists(Path.Combine(repoRoot, SchemaLayout.SchemaDir))) return null;
        var assembled = DesiredStateAssembler.Assemble(repoRoot).Replace("\r\n", "\n");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(assembled)));
    }

    /// <summary>Ordinary tables in application schemas, with their COPY-able (non-generated) columns.</summary>
    public static IReadOnlyList<TableRef> DiscoverTables(NpgsqlConnection conn)
    {
        const string sql = """
            SELECT n.nspname, c.relname, a.attname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid
            WHERE c.relkind = 'r'
              AND n.nspname <> ALL (@excluded)
              AND c.relname <> @efHistory
              AND a.attnum > 0 AND NOT a.attisdropped AND a.attgenerated = ''
            ORDER BY n.nspname, c.relname, a.attnum
            """;
        var result = new List<TableRef>();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("excluded", ExcludedSchemas);
        cmd.Parameters.AddWithValue("efHistory", SchemaLayout.EfHistoryTable);
        using var r = cmd.ExecuteReader();
        string? curSchema = null, curTable = null;
        List<string> cols = [];
        while (r.Read())
        {
            var (schema, table, col) = (r.GetString(0), r.GetString(1), r.GetString(2));
            if (schema != curSchema || table != curTable)
            {
                if (curTable is not null) result.Add(new TableRef(curSchema!, curTable, cols));
                (curSchema, curTable, cols) = (schema, table, []);
            }
            cols.Add(col);
        }
        if (curTable is not null) result.Add(new TableRef(curSchema!, curTable, cols));
        return result;
    }

    public static string QuoteIdent(string ident) => $"\"{ident.Replace("\"", "\"\"")}\"";
    public static string Qualify(string schema, string table) => $"{QuoteIdent(schema)}.{QuoteIdent(table)}";

    /// <summary>Dump file name for a table, safe on any filesystem.</summary>
    public static string FileFor(string schema, string table) => $"tables/{schema}.{table}.copy";

    private static DumpManifest.Table DumpTable(NpgsqlConnection conn, TableRef t, string outDir)
    {
        var relFile = FileFor(t.Schema, t.Name);
        var path = Path.Combine(outDir, relFile);
        var colList = string.Join(", ", t.Columns.Select(QuoteIdent));

        long rows = 0, bytes = 0;
        using var sha = SHA256.Create();
        using (var file = File.Create(path))
        using (var hashing = new CryptoStream(file, sha, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(hashing, new UTF8Encoding(false)))
        using (var reader = conn.BeginTextExport($"COPY {Qualify(t.Schema, t.Name)} ({colList}) TO STDOUT"))
        {
            var buf = new char[64 * 1024];
            int n;
            while ((n = reader.Read(buf, 0, buf.Length)) > 0)
            {
                writer.Write(buf, 0, n);
                for (var i = 0; i < n; i++)
                    if (buf[i] == '\n')
                        rows++;
            }
        }
        bytes = new FileInfo(path).Length;
        return new DumpManifest.Table(
            t.Schema, t.Name, t.Columns, rows, bytes,
            Convert.ToHexStringLower(sha.Hash!), relFile);
    }
}
