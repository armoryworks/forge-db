namespace Forge.Db.Commands;

/// <summary>
/// <c>apply --db &lt;url&gt; [--env name] [--yes] [--backup-taken] [--allow-destructive]</c> —
/// reconcile a live DB to desired state behind the safety gates (docs/DESIGN §4, §4.1). Captures the
/// plan SQL to <c>history/</c> BEFORE applying (an audit receipt, never replayed), then runs
/// pg-schema-diff with exactly the hazards the gates approved.
/// </summary>
public static class ApplyCommand
{
    public static int Run(
        string repoRoot, string dbUrl, string env,
        bool yes, bool backupTaken, bool allowDestructive)
    {
        var isDev = env.Equals("dev", StringComparison.OrdinalIgnoreCase)
                    || env.Equals("scratch", StringComparison.OrdinalIgnoreCase);

        var runner = new PgSchemaDiffRunner(DesiredStateAssembler.WriteTempDir(repoRoot));

        var plan = runner.Plan(dbUrl);
        if (!plan.Ok)
        {
            Console.Error.WriteLine("[apply] could not compute plan:");
            Console.Error.WriteLine((plan.StdErr + plan.StdOut).Trim());
            return 1;
        }
        if (PgSchemaDiffRunner.IsInSync(plan.StdOut))
        {
            Console.WriteLine("[apply] no changes — target already in sync. Nothing to do.");
            return 0;
        }

        var planSql = plan.StdOut;
        var hazards = PgSchemaDiffRunner.Hazards(planSql);

        var gate = DeployGates.Evaluate(planSql, isDev, yes, backupTaken, allowDestructive);
        if (!gate.Allowed)
        {
            Console.Error.WriteLine($"[apply] BLOCKED: {gate.Reason}");
            return 3;
        }
        Console.WriteLine($"[apply] gates: {gate.Reason}");
        if (hazards.Count > 0) Console.WriteLine($"[apply] allowing hazards: {string.Join(", ", hazards)}");

        var historyPath = CapturePlan(repoRoot, env, planSql);
        Console.WriteLine($"[apply] captured plan → {Path.GetRelativePath(repoRoot, historyPath)} (audit-only, never replayed)");

        var result = runner.Apply(dbUrl, hazards);
        Console.Write(result.StdOut);
        if (!result.Ok)
        {
            Console.Error.WriteLine("[apply] pg-schema-diff apply FAILED:");
            Console.Error.WriteLine(result.StdErr.Trim());
            return 1;
        }
        Console.WriteLine($"[apply] applied to env '{env}'.");
        return 0;
    }

    private static string CapturePlan(string repoRoot, string env, string planSql)
    {
        var dir = Path.Combine(repoRoot, "history");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var path = Path.Combine(dir, $"{stamp}-{Sanitize(env)}.sql");
        File.WriteAllText(path,
            "-- forge-db apply plan (audit receipt — NOT replayable; schema/ + pg-schema-diff are the source of truth)\n"
            + $"-- env: {env}    captured: {stamp}\n\n{planSql}\n");
        return path;
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
