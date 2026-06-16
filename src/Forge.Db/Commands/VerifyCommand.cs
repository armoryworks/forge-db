namespace Forge.Db.Commands;

/// <summary>
/// <c>verify --db &lt;url&gt;</c> — assert a live DB matches the desired state. Exits non-zero on any
/// drift (for CI). Two independent checks:
///   1. <c>pg-schema-diff plan</c> — an empty plan means tables/indexes/constraints/functions/
///      triggers/extensions all match. (pg-schema-diff covers everything; unlike Atlas there is no
///      object-kind it can't see.)
///   2. an explicit <c>pg_extension</c>/<c>pg_proc</c>/<c>pg_trigger</c> assertion — belt-and-
///      suspenders for the objects that burned us during the squash (docs/DESIGN §9 #1), and a guard
///      against pg-schema-diff's own "untrackable function dependency" caveat.
/// </summary>
public static class VerifyCommand
{
    public static int Run(string repoRoot, string dbUrl)
    {
        var ok = true;

        var runner = new PgSchemaDiffRunner(DesiredStateAssembler.WriteTempDir(repoRoot));
        var plan = runner.Plan(dbUrl);
        if (!plan.Ok)
        {
            ok = false;
            Console.Error.WriteLine("[verify] pg-schema-diff plan FAILED:");
            Console.Error.WriteLine(Indent((plan.StdErr + plan.StdOut).Trim()));
        }
        else if (PgSchemaDiffRunner.IsInSync(plan.StdOut))
        {
            Console.WriteLine("[verify] pg-schema-diff: in sync ✓ (tables, indexes, constraints, functions, triggers, extensions)");
        }
        else
        {
            ok = false;
            Console.Error.WriteLine("[verify] pg-schema-diff: DRIFT ✗ — plan would run:");
            Console.Error.WriteLine(Indent(plan.StdOut.Trim()));
        }

        var so = SchemaObjectVerifier.Verify(DbUrl.ToNpgsql(dbUrl), repoRoot);
        if (so.InSync)
        {
            Console.WriteLine("[verify] explicit pg_extension/pg_proc/pg_trigger check: in sync ✓");
        }
        else
        {
            ok = false;
            Console.Error.WriteLine("[verify] explicit extension/function/trigger check: DRIFT ✗ (§9 #1 guard)");
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

    private static void Report(string label, IReadOnlyList<string> names)
    {
        foreach (var n in names) Console.Error.WriteLine($"[verify]     {label}: {n}");
    }

    private static string Indent(string text) =>
        string.Join('\n', text.Split('\n').Select(l => "    " + l));
}
