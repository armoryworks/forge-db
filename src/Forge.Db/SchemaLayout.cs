namespace Forge.Db;

/// <summary>
/// Canonical on-disk layout of the desired-state schema tree (see docs/DESIGN.md §3).
/// One object per file, dacpac-style. INPUT (you edit) — distinct from history/ (OUTPUT).
/// </summary>
public static class SchemaLayout
{
    public const string SchemaDir = "schema";
    public const string Extensions = "schema/extensions";
    public const string Tables = "schema/tables";
    public const string Indexes = "schema/indexes";
    public const string Views = "schema/views";
    public const string Functions = "schema/functions";
    public const string Triggers = "schema/triggers";

    /// <summary>
    /// Ordered, applied-once scripts that run BEFORE the schema reconcile.
    ///
    /// <para>Exists for changes pg-schema-diff cannot express because it compares states rather than
    /// recording intent. A rename is the canonical case: the tool sees a table that is gone and a
    /// table that is new, and plans DROP + CREATE, which destroys every row. Renaming here first
    /// means the reconcile that follows sees the right names and plans only the additive delta.</para>
    ///
    /// <para>Runs before the plan is even computed, not merely before the apply — the plan is derived
    /// from live DB state, so a rename applied after planning would be reconciling against a shape
    /// that no longer exists.</para>
    ///
    /// <para>Same contract as <see cref="DataDir"/>: numbered, applied-once via the ledger, and
    /// authored to be idempotent anyway (<c>ALTER TABLE IF EXISTS</c>). Scripts must be safe to run
    /// against a database that is already at the target shape, because that is exactly what happens
    /// on the second deploy.</para>
    /// </summary>
    public const string PreMigrateDir = "premigrate";

    /// <summary>
    /// Ordered, applied-once backfill scripts (docs/DESIGN §6.1) — the one change-based area.
    /// Runs before <see cref="SeedDir"/> so a column added + backfilled precedes reference rows
    /// that depend on it. Applied by <see cref="DataSeedRunner"/>, NOT by pg-schema-diff.
    /// </summary>
    public const string DataDir = "data";

    /// <summary>
    /// Schema-adjacent reference/lookup rows (reference_data groups the app assumes exist).
    /// Applied like <see cref="DataDir"/> — ordered, applied-once, and idempotent-anyway — by
    /// <see cref="DataSeedRunner"/>. Runs after <see cref="DataDir"/>.
    /// </summary>
    public const string SeedDir = "seed";

    /// <summary>
    /// Version-controlled cleanup SQL for the clean-rebuild workflow (docs/DESIGN §6.2). Run by
    /// <c>import</c> after the data load — every import, NOT applied-once (unlike
    /// <see cref="DataDir"/>/<see cref="SeedDir"/>), so scripts must be idempotent by authoring.
    /// This is where "garbage" is defined: purge soft-deleted rows, expired tokens, orphans.
    /// </summary>
    public const string ScrubDir = "scrub";

    /// <summary>EF Core bookkeeping table — owned by EF, never part of forge-db's desired state.</summary>
    public const string EfHistoryTable = "__EFMigrationsHistory";

    public static readonly string[] AllSubDirs =
    [
        Extensions, Tables, Indexes, Views, Functions, Triggers,
    ];

    /// <summary>Enumerate every authored *.sql file across the schema tree (sorted, stable).</summary>
    public static IEnumerable<string> EnumerateSchemaFiles(string repoRoot)
    {
        var root = Path.Combine(repoRoot, SchemaDir);
        if (!Directory.Exists(root)) yield break;
        foreach (var f in Directory.EnumerateFiles(root, "*.sql", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
            yield return f;
    }
}
