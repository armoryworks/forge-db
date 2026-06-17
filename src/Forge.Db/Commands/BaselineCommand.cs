namespace Forge.Db.Commands;

/// <summary>
/// <c>baseline &lt;dump.sql&gt;</c> — one-time ingest of the squash's canonical
/// <c>pg_dump --schema-only</c> into the desired-state <c>schema/</c> tree (docs/DESIGN.md §2, §4).
/// </summary>
public static class BaselineCommand
{
    public static int Run(string repoRoot, string? dumpPath)
    {
        if (string.IsNullOrWhiteSpace(dumpPath) || !File.Exists(dumpPath))
        {
            Console.Error.WriteLine($"[baseline] dump not found: --dump '{dumpPath}'");
            return 2;
        }

        var dump = File.ReadAllText(dumpPath);
        var result = SqlDumpSplitter.Write(dump, repoRoot);

        Console.WriteLine($"[baseline] parsed {result.ObjectsParsed} objects from {Path.GetFileName(dumpPath)} "
            + $"(skipped {result.EfObjectsSkipped} EF bookkeeping objects)");
        foreach (var (dir, n) in result.FilesPerDir.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Console.WriteLine($"[baseline]   {dir,-18} {n,5} file(s)");
        Console.WriteLine("[baseline] schema/ tree written. Review the diff, then commit.");
        return 0;
    }
}
