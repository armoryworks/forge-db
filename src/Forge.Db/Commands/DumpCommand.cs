namespace Forge.Db.Commands;

/// <summary>
/// <c>dump --db &lt;url&gt; --out &lt;dir&gt;</c> — stream every application table's data to a dump
/// directory (read-only against the source; docs/DESIGN §6.2). The output pairs with
/// <c>import</c> for the clean-rebuild workflow: dump the old system, provision a fresh one with
/// <c>apply</c>, load the dump back minus the garbage.
/// </summary>
public static class DumpCommand
{
    public static int Run(string repoRoot, string dbUrl, string outDir)
    {
        if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
        {
            Console.Error.WriteLine($"[dump] refusing to write into non-empty directory: {outDir}");
            return 2;
        }

        var manifest = DataDumper.Run(repoRoot, dbUrl, outDir);
        var rows = manifest.Tables.Sum(t => t.Rows);
        var bytes = manifest.Tables.Sum(t => t.Bytes);
        Console.WriteLine($"[dump] {manifest.Tables.Count} tables, {rows:n0} rows, {bytes / (1024.0 * 1024):n1} MiB → {outDir}");
        if (manifest.SchemaFingerprint is null)
            Console.Error.WriteLine("[dump] note: run inside a forge-db repo to stamp the schema fingerprint (import skew check).");
        return 0;
    }
}
