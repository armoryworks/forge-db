namespace Forge.Db.Commands;

/// <summary>
/// <c>apply --db &lt;url&gt; --dev-url &lt;url&gt; [--env name] [--yes] [--backup-taken]
/// [--allow-destructive]</c> — reconcile a live DB to desired state, behind the safety gates
/// (docs/DESIGN §4, §4.1). Always captures the plan SQL to <c>history/</c> BEFORE applying — an
/// audit receipt, never replayed.
/// </summary>
public static class ApplyCommand
{
    public static int Run(
        string repoRoot, string dbUrl, string devUrl, string env,
        bool yes, bool backupTaken, bool allowDestructive)
    {
        var isDev = env.Equals("dev", StringComparison.OrdinalIgnoreCase)
                    || env.Equals("scratch", StringComparison.OrdinalIgnoreCase);

        DevDbBootstrap.EnsureExtensions(devUrl, repoRoot);
        var atlas = new AtlasRunner(DesiredStateAssembler.WriteTemp(repoRoot), devUrl);

        var plan = atlas.Plan(dbUrl);
        var planSql = plan.StdOut.Trim();
        if (!plan.Ok && planSql.Length == 0)
        {
            Console.Error.WriteLine("[apply] could not compute plan:");
            Console.Error.WriteLine(plan.StdErr.Trim());
            return 1;
        }
        if (planSql.Length == 0 || planSql.Contains("Schemas are synced", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[apply] no changes — target already in sync. Nothing to do.");
            return 0;
        }

        var gate = DeployGates.Evaluate(planSql, isDev, yes, backupTaken, allowDestructive);
        if (!gate.Allowed)
        {
            Console.Error.WriteLine($"[apply] BLOCKED: {gate.Reason}");
            return 3;
        }
        Console.WriteLine($"[apply] gates: {gate.Reason}");

        var historyPath = CapturePlan(repoRoot, env, planSql);
        Console.WriteLine($"[apply] captured plan → {Path.GetRelativePath(repoRoot, historyPath)} (audit-only, never replayed)");

        var result = atlas.Apply(dbUrl, autoApprove: true);
        Console.Write(result.StdOut);
        if (!result.Ok)
        {
            Console.Error.WriteLine("[apply] atlas apply FAILED:");
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
            $"-- forge-db apply plan (audit receipt — NOT replayable; schema/ + Atlas are the source of truth)\n"
            + $"-- env: {env}    captured: {stamp}\n\n{planSql}\n");
        return path;
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
