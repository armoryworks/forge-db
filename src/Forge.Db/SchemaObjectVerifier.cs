using Npgsql;

namespace Forge.Db;

/// <summary>
/// Belt-and-suspenders comparison of <b>extensions</b>, <b>functions</b>, and <b>triggers</b>
/// (docs/DESIGN §9 #1). pg-schema-diff DOES diff all three, so this is no longer the sole safety net
/// — but we keep it because the squash taught us a silent diff gap on the ledger immutability
/// triggers is catastrophic, and pg-schema-diff itself documents that non-SQL function dependencies
/// are "untrackable". Each authored file under schema/{extensions,functions,triggers} is one desired
/// object (filename == object name); the live DB's non-extension, non-internal objects must match
/// exactly. Extension-owned functions (pgvector) and FK-internal triggers are excluded so they never
/// read as spurious drift.
/// </summary>
public static class SchemaObjectVerifier
{
    public sealed record Result(
        IReadOnlyList<string> MissingExtensions, IReadOnlyList<string> ExtraExtensions,
        IReadOnlyList<string> MissingFunctions, IReadOnlyList<string> ExtraFunctions,
        IReadOnlyList<string> MissingTriggers, IReadOnlyList<string> ExtraTriggers)
    {
        public bool InSync =>
            MissingExtensions.Count == 0 && ExtraExtensions.Count == 0 &&
            MissingFunctions.Count == 0 && ExtraFunctions.Count == 0 &&
            MissingTriggers.Count == 0 && ExtraTriggers.Count == 0;
    }

    public static Result Verify(string connString, string repoRoot)
    {
        var desiredExts = DesiredNames(repoRoot, SchemaLayout.Extensions);
        var desiredFns = DesiredNames(repoRoot, SchemaLayout.Functions);
        var desiredTrgs = DesiredNames(repoRoot, SchemaLayout.Triggers);

        var liveExts = new HashSet<string>(StringComparer.Ordinal);
        var liveFns = new HashSet<string>(StringComparer.Ordinal);
        var liveTrgs = new HashSet<string>(StringComparer.Ordinal);

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        // 'plpgsql' is a built-in language extension present on every DB — not forge-db's to own.
        Collect(conn, "SELECT extname FROM pg_extension WHERE extname <> 'plpgsql'", liveExts);

        Collect(conn, """
            SELECT p.proname
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.prokind = 'f'
              AND NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = p.oid AND d.deptype = 'e')
            """, liveFns);

        Collect(conn, """
            SELECT t.tgname
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT t.tgisinternal AND n.nspname = 'public'
            """, liveTrgs);

        return new Result(
            Diff(desiredExts, liveExts).Missing, Diff(desiredExts, liveExts).Extra,
            Diff(desiredFns, liveFns).Missing, Diff(desiredFns, liveFns).Extra,
            Diff(desiredTrgs, liveTrgs).Missing, Diff(desiredTrgs, liveTrgs).Extra);
    }

    private static (List<string> Missing, List<string> Extra) Diff(HashSet<string> desired, HashSet<string> live) =>
        (desired.Except(live).OrderBy(x => x, StringComparer.Ordinal).ToList(),
         live.Except(desired).OrderBy(x => x, StringComparer.Ordinal).ToList());

    private static void Collect(NpgsqlConnection conn, string sql, HashSet<string> into)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) into.Add(r.GetString(0));
    }

    private static HashSet<string> DesiredNames(string repoRoot, string subDir)
    {
        var dir = Path.Combine(repoRoot, subDir);
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(dir)) return set;
        foreach (var f in Directory.EnumerateFiles(dir, "*.sql"))
            set.Add(Path.GetFileNameWithoutExtension(f));
        return set;
    }
}
