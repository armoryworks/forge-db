using System.Text.RegularExpressions;

namespace Forge.Db;

/// <summary>
/// Forge-specific safety that lives ABOVE Atlas's diff (docs/DESIGN.md §4): block possibly-lossy
/// changes by default (dacpac <c>BlockOnPossibleDataLoss</c> parity), and never auto-apply against
/// a non-dev target without an explicit confirmation. Atlas decides correctness; these gates decide
/// whether we are allowed to run it at all.
/// </summary>
public static partial class DeployGates
{
    [GeneratedRegex(@"\b(DROP\s+TABLE|DROP\s+COLUMN|DROP\s+CONSTRAINT|DROP\s+INDEX|DROP\s+SCHEMA|DROP\s+FUNCTION|DROP\s+TRIGGER|ALTER\s+TABLE[^;]*\bDROP\b)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DestructiveRe();

    /// <summary>Statements in the plan that would (or might) lose data/objects.</summary>
    public static IReadOnlyList<string> DestructiveStatements(string planSql) =>
        DestructiveRe().Matches(planSql).Select(m => m.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public sealed record GateResult(bool Allowed, string Reason);

    /// <summary>
    /// Evaluate apply gates. <paramref name="isDev"/> targets skip the confirm/backup gates (dev DBs
    /// are disposable, docs/DESIGN §3.5). Destructive statements always require <c>--allow-destructive</c>.
    /// </summary>
    public static GateResult Evaluate(string planSql, bool isDev, bool confirmed, bool backupTaken, bool allowDestructive)
    {
        var destructive = DestructiveStatements(planSql);
        if (destructive.Count > 0 && !allowDestructive)
            return new GateResult(false,
                $"plan contains {destructive.Count} possibly-destructive statement(s) and --allow-destructive was not passed: "
                + string.Join("; ", destructive.Take(5)));

        if (isDev) return new GateResult(true, "dev target — confirm/backup gates skipped");

        if (!confirmed)
            return new GateResult(false, "non-dev target requires explicit --yes confirmation");
        if (!backupTaken)
            return new GateResult(false, "non-dev target requires a fresh backup (--backup-taken)");

        return new GateResult(true, "gates passed");
    }
}
