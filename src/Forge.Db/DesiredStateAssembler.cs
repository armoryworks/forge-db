using System.Text;

namespace Forge.Db;

/// <summary>
/// Assembles the per-object <c>schema/</c> tree into a single ordered desired-state SQL file for
/// pg-schema-diff (<c>--to-dir</c>). Necessary because pg-schema-diff applies the desired DDL to its
/// auto-managed temp DB in statement order — it does not topologically sort — so the statements must
/// already be in dependency order. We emit:
///   extensions → tables (CREATE TABLE + identity + PK/UQ/CK) → all FKs → indexes → functions →
///   triggers → EF history keep-alive
/// FK constraints are split out of the table files and emitted after every table exists, which
/// dissolves circular-FK ordering problems. The file ordering in <c>schema/</c> is for humans; this
/// is the machine ordering. (Unlike Atlas's free tier, pg-schema-diff runs CREATE EXTENSION and
/// manages functions/triggers, so every object kind belongs here — no engine blind spots to dodge.)
/// </summary>
public static class DesiredStateAssembler
{
    // EF Core's standard migration-history table (EF 10). Injected into the desired state so the
    // diff engine never plans to drop it; never written to the committed schema/ tree.
    private const string EfHistoryKeepAlive = """
        CREATE TABLE public."__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL,
            "ProductVersion" character varying(32) NOT NULL
        );
        ALTER TABLE ONLY public."__EFMigrationsHistory"
            ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");
        """;

    public static string Assemble(string repoRoot)
    {
        var sb = new StringBuilder();
        var fks = new StringBuilder();

        // Extensions first: pg-schema-diff applies the desired DDL to its own auto-managed temp DB
        // to compute the diff, so CREATE EXTENSION must run there for the vector-typed columns to
        // resolve. (pg-schema-diff is MIT/FOSS and runs CREATE EXTENSION without any account — the
        // reason we left Atlas; see docs/DESIGN §4.)
        AppendDir(sb, Path.Combine(repoRoot, SchemaLayout.Extensions));

        // Tables: separate FK constraints (emitted last) from the rest.
        foreach (var file in SqlFiles(Path.Combine(repoRoot, SchemaLayout.Tables)))
            foreach (var stmt in SplitStatements(File.ReadAllText(file)))
                (stmt.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ? fks : sb)
                    .Append(stmt).Append("\n\n");

        sb.Append(fks);

        AppendDir(sb, Path.Combine(repoRoot, SchemaLayout.Indexes));

        // Functions then triggers (triggers depend on functions). pg-schema-diff manages both, so
        // emitting them keeps the diff a true no-op and means pg-schema-diff natively covers the
        // objects Atlas's free tier could not even see (§9 #1). SchemaObjectVerifier still
        // double-checks extensions/functions/triggers as belt-and-suspenders.
        AppendDir(sb, Path.Combine(repoRoot, SchemaLayout.Functions));
        AppendDir(sb, Path.Combine(repoRoot, SchemaLayout.Triggers));

        // EF owns __EFMigrationsHistory — it is deliberately NOT in the committed schema/ tree. But
        // the diff engine would otherwise see a live table absent from desired state and plan a DROP.
        // Inject its exact DDL as a keep-alive so the plan stays a no-op. (When the §6 cutover removes
        // MigrateAsync the table becomes vestigial and this keep-alive can go.)
        sb.Append('\n').Append(EfHistoryKeepAlive).Append('\n');

        return sb.ToString();
    }

    /// <summary>
    /// Assemble and write to a fresh temp directory as a single <c>desired.sql</c>, returning the
    /// directory — the shape pg-schema-diff's <c>--to-dir</c> expects.
    /// </summary>
    public static string WriteTempDir(string repoRoot)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"forge-db-desired-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "desired.sql"), Assemble(repoRoot));
        return dir;
    }

    private static void AppendDir(StringBuilder sb, string dir)
    {
        foreach (var file in SqlFiles(dir))
            sb.Append(File.ReadAllText(file).TrimEnd()).Append("\n\n");
    }

    private static IEnumerable<string> SqlFiles(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.sql").OrderBy(p => p, StringComparer.Ordinal)
            : [];

    /// <summary>
    /// Split a table/index file into complete statements (statement ends at a line whose trimmed
    /// text ends with ';'). Safe here because these files never contain dollar-quoted bodies — only
    /// functions/triggers do, and those are emitted whole, not split.
    /// </summary>
    private static IEnumerable<string> SplitStatements(string sql)
    {
        var cur = new StringBuilder();
        foreach (var line in sql.Replace("\r\n", "\n").Split('\n'))
        {
            cur.Append(line).Append('\n');
            if (line.TrimEnd().EndsWith(';'))
            {
                var stmt = cur.ToString().Trim();
                if (stmt.Length > 0) yield return stmt;
                cur.Clear();
            }
        }
        var tail = cur.ToString().Trim();
        if (tail.Length > 0) yield return tail;
    }
}
