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
