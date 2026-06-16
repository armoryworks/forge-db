namespace Forge.Db.Commands;

/// <summary>
/// <c>plan --db &lt;url&gt; --dev-url &lt;url&gt;</c> — render the SQL Atlas WOULD run to reconcile the
/// target to desired state (docs/DESIGN §4). Pure read: no mutation. Flags any destructive
/// statements so the operator sees them before an <c>apply</c>.
/// </summary>
public static class PlanCommand
{
    public static int Run(string repoRoot, string dbUrl, string devUrl)
    {
        DevDbBootstrap.EnsureExtensions(devUrl, repoRoot);
        var atlas = new AtlasRunner(DesiredStateAssembler.WriteTemp(repoRoot), devUrl);
        var plan = atlas.Plan(dbUrl);
        var sql = plan.StdOut.Trim();

        if (!plan.Ok && sql.Length == 0)
        {
            Console.Error.WriteLine("[plan] atlas failed:");
            Console.Error.WriteLine(plan.StdErr.Trim());
            return 1;
        }

        if (sql.Length == 0 || sql.Contains("Schemas are synced", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[plan] no changes — target already matches desired state.");
            return 0;
        }

        Console.WriteLine("[plan] Atlas would apply:");
        Console.WriteLine(sql);

        var destructive = DeployGates.DestructiveStatements(sql);
        if (destructive.Count > 0)
        {
            Console.WriteLine($"[plan] ⚠ {destructive.Count} possibly-destructive statement(s) — apply requires --allow-destructive:");
            foreach (var d in destructive) Console.WriteLine($"[plan]     {d}");
        }
        return 0;
    }
}
