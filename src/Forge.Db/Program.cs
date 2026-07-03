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

        case "assemble":
        {
            var outPath = opts.GetValueOrDefault("out") ?? Path.Combine(repoRoot, "desired.local.sql");
            File.WriteAllText(outPath, DesiredStateAssembler.Assemble(repoRoot));
            Console.WriteLine($"[assemble] wrote desired state → {outPath}");
            return 0;
        }

        case "plan":
            return PlanCommand.Run(repoRoot, Required("db"));

        case "verify":
            return VerifyCommand.Run(repoRoot, Required("db"));

        case "apply":
            return ApplyCommand.Run(repoRoot, Required("db"),
                opts.GetValueOrDefault("env", "dev"),
                opts.ContainsKey("yes"), opts.ContainsKey("backup-taken"), opts.ContainsKey("allow-destructive"));

        case "version":
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            Console.WriteLine($"forge-db {v.Major}.{v.Minor}.{v.Build}  (repo={repoRoot})");
            var engine = ProcessRunner.Run(Environment.GetEnvironmentVariable("PG_SCHEMA_DIFF_BIN") ?? "pg-schema-diff", ["version"]);
            Console.WriteLine($"engine:  pg-schema-diff {engine.StdOut.Trim()}");
            return 0;
        }

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
        if (Directory.Exists(Path.Combine(dir.FullName, SchemaLayout.SchemaDir)))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

static void PrintHelp()
{
    Console.WriteLine("""
        forge-db — declarative Postgres schema harness over pg-schema-diff (stripe/pg-schema-diff)

        Usage:
          forge-db baseline --dump <pg_dump.sql> [--repo <dir>]
              Split a canonical pg_dump --schema-only into the schema/ tree (one object per file).

          forge-db assemble [--out <file>] [--repo <dir>]
              Assemble schema/ into a single ordered desired-state SQL (debug / inspection).

          forge-db plan    --db <url> [--repo <dir>]
              Show the migration SQL pg-schema-diff would run to reconcile <db> to schema/. No mutation.

          forge-db verify  --db <url> [--repo <dir>]
              Assert <db> matches schema/ — pg-schema-diff plan PLUS an explicit
              pg_extension/pg_proc/pg_trigger check. Exit non-zero on drift (for CI).

          forge-db apply   --db <url> [--env name] [--yes] [--backup-taken] [--allow-destructive]
              Reconcile <db> to schema/ behind safety gates; captures the plan to history/. Then
              runs any pending data/ + seed/ scripts once (applied-once ledger; DESIGN §6.1).

        --db is a Postgres connection string, e.g.
          postgres://postgres:pw@127.0.0.1:55432/forge?sslmode=disable
        The connecting user needs CREATEDB (pg-schema-diff provisions its own temp database).
        """);
}
