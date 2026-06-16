using System.Text;

namespace Forge.Db;

/// <summary>
/// Assembles the per-object <c>schema/</c> tree into a single ordered desired-state SQL file for
/// Atlas. Two reasons this is necessary: (1) Atlas's <c>file://</c> source does not recurse into
/// subdirectories, and (2) Atlas loads the desired state by executing it against its dev DB, so the
/// statements must apply in dependency order. We emit only the objects Atlas (free tier) can
/// process — tables and indexes — in this order:
///   tables (CREATE TABLE + identity + PK/UQ/CK) → all FKs → indexes
/// Extensions, functions, and triggers are excluded (Atlas-blind-spot objects forge-db owns; see
/// SchemaObjectVerifier). FK constraints are split out of the table files and emitted after every
/// table exists, which
/// dissolves circular-FK ordering problems. The file ordering in <c>schema/</c> is for humans; this
/// is the machine ordering.
/// </summary>
public static class DesiredStateAssembler
{
    public static string Assemble(string repoRoot)
    {
        var sb = new StringBuilder();
        var fks = new StringBuilder();

        // NOTE: extensions are intentionally NOT emitted. The Atlas free tier rejects CREATE
        // EXTENSION ("available to logged-in users only"), so — like triggers/functions — extensions
        // are an Atlas blind spot forge-db owns explicitly: the dev DB is pre-seeded with them
        // (DevDbBootstrap) and the live DB is checked by SchemaObjectVerifier, not Atlas.

        // Tables: separate FK constraints (emitted last) from the rest.
        foreach (var file in SqlFiles(Path.Combine(repoRoot, SchemaLayout.Tables)))
            foreach (var stmt in SplitStatements(File.ReadAllText(file)))
                (stmt.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ? fks : sb)
                    .Append(stmt).Append("\n\n");

        sb.Append(fks);

        AppendDir(sb, Path.Combine(repoRoot, SchemaLayout.Indexes));

        // extensions, functions, and triggers are NOT emitted: the Atlas free tier rejects all three
        // ("available to logged-in users only"). They are forge-db-owned and verified explicitly by
        // SchemaObjectVerifier — which is exactly the §9 #1 split (Atlas covers tables/indexes/
        // constraints; forge-db covers the objects Atlas can't see).

        return sb.ToString();
    }

    /// <summary>Assemble and write to a temp .sql file; returns its path (caller may delete).</summary>
    public static string WriteTemp(string repoRoot)
    {
        var path = Path.Combine(Path.GetTempPath(), $"forge-db-desired-{Guid.NewGuid():N}.sql");
        File.WriteAllText(path, Assemble(repoRoot));
        return path;
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
