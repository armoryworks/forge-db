namespace Forge.Db.Commands;

/// <summary>
/// <c>verify --db &lt;url&gt; --dev-url &lt;url&gt;</c> — assert a live DB matches the desired state.
/// Two independent checks because Atlas's diff alone is NOT proof of equivalence (docs/DESIGN §9 #1):
///   1. <c>atlas schema diff</c> for tables/columns/indexes/constraints.
///   2. explicit <c>pg_proc</c>/<c>pg_trigger</c> comparison for functions + triggers.
/// Exits non-zero on any drift (for CI).
/// </summary>
public static class VerifyCommand
{
    public static int Run(string repoRoot, string dbUrl, string devUrl)
    {
        var ok = true;

        DevDbBootstrap.EnsureExtensions(devUrl, repoRoot);
        var atlas = new AtlasRunner(DesiredStateAssembler.WriteTemp(repoRoot), devUrl);
        var diff = atlas.Diff(dbUrl);
        var atlasSynced = diff.Ok && IsEmptyDiff(diff.StdOut);
        if (atlasSynced)
        {
            Console.WriteLine("[verify] atlas schema diff: in sync ✓");
        }
        else
        {
            ok = false;
            Console.Error.WriteLine("[verify] atlas schema diff: DRIFT ✗");
            var detail = (diff.StdOut + diff.StdErr).Trim();
            if (detail.Length > 0) Console.Error.WriteLine(Indent(detail));
        }

        var so = SchemaObjectVerifier.Verify(DbUrl.ToNpgsql(dbUrl), repoRoot);
        if (so.InSync)
        {
            Console.WriteLine("[verify] extensions + functions + triggers (Atlas blind spots): in sync ✓");
        }
        else
        {
            ok = false;
            Console.Error.WriteLine("[verify] extensions/functions/triggers: DRIFT ✗ (Atlas does NOT catch this — §9 #1)");
            Report("missing extension (in schema/, absent in DB)", so.MissingExtensions);
            Report("extra extension (in DB, not in schema/)", so.ExtraExtensions);
            Report("missing function (in schema/, absent in DB)", so.MissingFunctions);
            Report("extra function (in DB, not in schema/)", so.ExtraFunctions);
            Report("missing trigger (in schema/, absent in DB)", so.MissingTriggers);
            Report("extra trigger (in DB, not in schema/)", so.ExtraTriggers);
        }

        Console.WriteLine(ok ? "[verify] PASS — live DB matches desired state." : "[verify] FAIL — drift detected.");
        return ok ? 0 : 1;
    }

    private static bool IsEmptyDiff(string stdout)
    {
        var s = stdout.Trim();
        return s.Length == 0 || s.Contains("Schemas are synced", StringComparison.OrdinalIgnoreCase);
    }

    private static void Report(string label, IReadOnlyList<string> names)
    {
        foreach (var n in names) Console.Error.WriteLine($"[verify]     {label}: {n}");
    }

    private static string Indent(string text) =>
        string.Join('\n', text.Split('\n').Select(l => "    " + l));
}
