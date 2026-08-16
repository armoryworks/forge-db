namespace Forge.Db.Commands;

/// <summary>
/// <c>plan --db &lt;url&gt;</c> — render the migration SQL pg-schema-diff WOULD run to reconcile the
/// target to desired state (docs/DESIGN §4). Pure read: no mutation. Surfaces the hazards
/// pg-schema-diff flagged so the operator sees them before an <c>apply</c>.
///
/// <para><b>Pending pre-migrate scripts make this plan provisional.</b> The plan is computed against
/// live DB state, and <c>apply</c> runs the pre-migrate phase first — so if a rename is pending, the
/// plan shown here is the one that would run WITHOUT it, complete with the DROP + CREATE the rename
/// exists to avoid. Planning stays read-only rather than quietly applying those scripts, so this
/// says so loudly instead. Reading that hazard as real and reaching for
/// <c>--allow-destructive</c> is precisely the mistake to avoid.</para>
/// </summary>
public static class PlanCommand
{
    public static int Run(string repoRoot, string dbUrl)
    {
        var pendingPreMigrate = PendingPreMigrate(repoRoot, dbUrl);

        var runner = new PgSchemaDiffRunner(DesiredStateAssembler.WriteTempDir(repoRoot));
        var plan = runner.Plan(dbUrl);

        if (!plan.Ok)
        {
            Console.Error.WriteLine("[plan] pg-schema-diff failed:");
            Console.Error.WriteLine((plan.StdErr + plan.StdOut).Trim());
            return 1;
        }

        if (pendingPreMigrate.Count > 0)
        {
            Console.WriteLine(
                $"[plan] ⚠ {pendingPreMigrate.Count} pre-migrate script(s) have not run on this target:");
            foreach (var name in pendingPreMigrate) Console.WriteLine($"[plan]     {name}");
            Console.WriteLine(
                "[plan]   `apply` runs those FIRST, so the plan below is not what apply will do. "
                + "Hazards it reports may be exactly what those scripts exist to prevent.");
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
        {
            Console.WriteLine($"[plan] ⚠ hazards: {string.Join(", ", hazards)} — apply must --allow-hazards these; "
                + "DELETES_DATA additionally requires --allow-destructive.");

            if (pendingPreMigrate.Count > 0)
                Console.WriteLine(
                    "[plan]   Re-read the pre-migrate warning above before allowing anything destructive.");
        }
        return 0;
    }

    /// <summary>
    /// Pre-migrate scripts on disk that the target's ledger has not recorded.
    /// Read-only, and deliberately forgiving: an unreachable or ledger-less
    /// database yields an empty list rather than failing the plan, because
    /// planning must keep working against a target that has never been applied to.
    /// </summary>
    private static IReadOnlyList<string> PendingPreMigrate(string repoRoot, string dbUrl)
    {
        try
        {
            var all = DataSeedRunner.Discover(repoRoot, SchemaLayout.PreMigrateDir);
            if (all.Count == 0) return [];

            var applied = DataSeedRunner.LoadAppliedNames(DbUrl.ToNpgsql(dbUrl));
            return DataSeedRunner.Pending(all, applied).Select(s => s.Name).ToList();
        }
        catch
        {
            return [];
        }
    }
}
