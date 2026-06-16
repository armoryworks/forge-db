using Forge.Db;
using Forge.Db.Commands;

if (args.Length == 0)
{
    PrintHelp();
    return 64;
}

var verb = args[0];
var boolFlags = new HashSet<string>(StringComparer.Ordinal) { "yes", "backup-taken", "allow-destructive" };
var opts = ParseFlags(args.Skip(1), boolFlags);

var repoRoot = ResolveRepoRoot(opts.GetValueOrDefault("repo"));

try
{
    switch (verb)
    {
        case "baseline":
            return BaselineCommand.Run(repoRoot, opts.GetValueOrDefault("dump"));

        case "plan":
            return PlanCommand.Run(repoRoot, Required("db"), Required("dev-url"));

        case "verify":
            return VerifyCommand.Run(repoRoot, Required("db"), Required("dev-url"));

        case "apply":
            return ApplyCommand.Run(repoRoot, Required("db"), Required("dev-url"),
                opts.GetValueOrDefault("env", "dev"),
                opts.ContainsKey("yes"), opts.ContainsKey("backup-taken"), opts.ContainsKey("allow-destructive"));

        case "version":
            Console.WriteLine($"forge-db harness; repo={repoRoot}");
            Console.Write(ProcessRunner.Run(Environment.GetEnvironmentVariable("ATLAS_BIN") ?? "atlas", ["version"]).StdOut);
            return 0;

        default:
            Console.Error.WriteLine($"unknown verb: {verb}");
            PrintHelp();
            return 64;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[forge-db] {verb} failed: {ex.Message}");
    return 1;
}

string Required(string key)
{
    if (opts.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
    throw new ArgumentException($"missing required --{key}");
}

static Dictionary<string, string> ParseFlags(IEnumerable<string> tokens, HashSet<string> boolFlags)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    var list = tokens.ToList();
    for (var i = 0; i < list.Count; i++)
    {
        var tok = list[i];
        if (!tok.StartsWith("--", StringComparison.Ordinal)) continue;
        var key = tok[2..];
        if (boolFlags.Contains(key))
        {
            result[key] = "true";
        }
        else if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = list[++i];
        }
        else
        {
            result[key] = "true";
        }
    }
    return result;
}

static string ResolveRepoRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot)) return Path.GetFullPath(explicitRoot);
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "atlas.hcl")) ||
            Directory.Exists(Path.Combine(dir.FullName, SchemaLayout.SchemaDir)))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

static void PrintHelp()
{
    Console.WriteLine("""
        forge-db — declarative Postgres schema harness over Atlas

        Usage:
          forge-db baseline --dump <pg_dump.sql> [--repo <dir>]
              Split a canonical pg_dump --schema-only into the schema/ tree (one object per file).

          forge-db plan    --db <url> --dev-url <url> [--repo <dir>]
              Show the SQL Atlas would run to reconcile <db> to schema/. No mutation.

          forge-db verify  --db <url> --dev-url <url> [--repo <dir>]
              Assert <db> matches schema/ — atlas diff PLUS explicit pg_proc/pg_trigger check.
              Exit non-zero on drift (for CI).

          forge-db apply   --db <url> --dev-url <url> [--env name]
                           [--yes] [--backup-taken] [--allow-destructive]
              Reconcile <db> to schema/ behind safety gates; captures the plan to history/.

        URLs are Atlas Postgres URLs, e.g.
          postgres://postgres:pw@127.0.0.1:55432/forge?sslmode=disable
        """);
}
