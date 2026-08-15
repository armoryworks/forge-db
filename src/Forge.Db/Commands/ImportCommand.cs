using System.Text.Json;

namespace Forge.Db.Commands;

/// <summary>
/// <c>import --db &lt;url&gt; --from &lt;dir&gt; [--env name] [--yes --backup-taken]
/// [--exclude globs] [--skip-scrub] [--allow-fk-orphans]</c> — load a <c>dump</c> into a freshly
/// provisioned database, cleaning garbage on the way (docs/DESIGN §6.2). Destructive on the target
/// (selected tables are truncated first), so non-dev targets sit behind the same confirm+backup
/// posture as a schema <c>apply</c>. FK orphans surfaced by the validation pass fail the import
/// (exit 4) unless <c>--allow-fk-orphans</c> — they are the garbage this workflow exists to catch.
/// </summary>
public static class ImportCommand
{
    public static int Run(
        string repoRoot, string dbUrl, string dumpDir, string env,
        bool yes, bool backupTaken, string? excludeCsv, bool skipScrub, bool allowFkOrphans)
    {
        var isDev = env.Equals("dev", StringComparison.OrdinalIgnoreCase)
                    || env.Equals("scratch", StringComparison.OrdinalIgnoreCase);
        if (!isDev && !(yes && backupTaken))
        {
            Console.Error.WriteLine(
                "[import] BLOCKED: import truncates every selected table on the target; a non-dev " +
                "target requires --yes --backup-taken (same posture as a schema apply).");
            return 3;
        }

        var excludes = TableGlob.Parse(excludeCsv);
        if (excludes.Count > 0)
            Console.WriteLine($"[import] excluding: {string.Join(", ", excludes)}");

        var result = DataImporter.Run(repoRoot, dbUrl, dumpDir, excludes, skipScrub);

        var receiptPath = WriteReceipt(repoRoot, env, dumpDir, result);
        Console.WriteLine($"[import] receipt → {Path.GetRelativePath(repoRoot, receiptPath)}");
        Console.WriteLine(
            $"[import] {result.Loaded.Count} tables loaded ({result.Loaded.Sum(t => t.Rows):n0} rows), " +
            $"{result.Excluded.Count} excluded, {result.MissingInTarget.Count} missing in target, " +
            $"{result.ScrubScripts.Count} scrub script(s) run.");

        if (result.Orphans.Count > 0)
        {
            Console.Error.WriteLine($"[import] {result.Orphans.Count} foreign key(s) have orphaned child rows:");
            foreach (var v in result.Orphans)
                Console.Error.WriteLine($"[import]   {v.ChildTable} → {v.ParentTable}  ({v.Constraint}): {v.Rows:n0} orphan row(s)");
            if (!allowFkOrphans)
            {
                Console.Error.WriteLine(
                    "[import] FAILED: orphans mean child rows reference parents that didn't make the trip. " +
                    "Add a scrub/ rule (or re-include the excluded parent) and re-run, or pass " +
                    "--allow-fk-orphans to accept them as-is.");
                return 4;
            }
            Console.Error.WriteLine("[import] --allow-fk-orphans passed — accepting them as-is.");
        }

        Console.WriteLine("[import] done.");
        return 0;
    }

    /// <summary>Audit receipt alongside apply's plan captures in <c>history/</c> — an OUTPUT, never replayed.</summary>
    private static string WriteReceipt(string repoRoot, string env, string dumpDir, DataImporter.Result result)
    {
        var dir = Path.Combine(repoRoot, "history");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        var path = Path.Combine(dir, $"{stamp}-import-{Sanitize(env)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            importedAtUtc = stamp,
            env,
            dumpDir = Path.GetFullPath(dumpDir),
            loaded = result.Loaded,
            excluded = result.Excluded,
            missingInTarget = result.MissingInTarget,
            scrubScripts = result.ScrubScripts,
            fkOrphans = result.Orphans,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
