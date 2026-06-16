namespace Forge.Db.Commands;

/// <summary>
/// <c>plan --db &lt;url&gt;</c> — render the migration SQL pg-schema-diff WOULD run to reconcile the
/// target to desired state (docs/DESIGN §4). Pure read: no mutation. Surfaces the hazards
/// pg-schema-diff flagged so the operator sees them before an <c>apply</c>.
/// </summary>
public static class PlanCommand
{
    public static int Run(string repoRoot, string dbUrl)
    {
        var runner = new PgSchemaDiffRunner(DesiredStateAssembler.WriteTempDir(repoRoot));
        var plan = runner.Plan(dbUrl);

        if (!plan.Ok)
        {
            Console.Error.WriteLine("[plan] pg-schema-diff failed:");
            Console.Error.WriteLine((plan.StdErr + plan.StdOut).Trim());
            return 1;
        }

        if (PgSchemaDiffRunner.IsInSync(plan.StdOut))
        {
            Console.WriteLine("[plan] no changes — target already matches desired state.");
            return 0;
        }

        Console.WriteLine("[plan] pg-schema-diff would apply:");
        Console.WriteLine(plan.StdOut.TrimEnd());

        var hazards = PgSchemaDiffRunner.Hazards(plan.StdOut);
        if (hazards.Count > 0)
            Console.WriteLine($"[plan] ⚠ hazards: {string.Join(", ", hazards)} — apply must --allow-hazards these; "
                + "DELETES_DATA additionally requires --allow-destructive.");
        return 0;
    }
}
