using System.Text.RegularExpressions;

namespace Forge.Db;

/// <summary>
/// Orchestrates the <a href="https://github.com/stripe/pg-schema-diff">stripe/pg-schema-diff</a>
/// CLI (MIT, no account/registration — the reason we left Atlas). pg-schema-diff owns the diff
/// correctness; this type only decides what to invoke. The binary is resolved from PATH (we ship it
/// in <c>~/.local/bin</c>); override with the <c>PG_SCHEMA_DIFF_BIN</c> env var.
///
/// <para><b>Model:</b> <c>plan</c> produces the migration SQL (the diff) directly — an empty plan
/// means the target already matches the desired state. There is no separate dev-DB to provision;
/// pg-schema-diff auto-manages a temp DB on the target server (the connecting user needs CREATEDB).
/// The desired state (incl. <c>CREATE EXTENSION vector</c>) is applied to that temp DB, so the
/// vector-typed tables resolve.</para>
/// </summary>
public sealed partial class PgSchemaDiffRunner
{
    private readonly string _bin;
    private readonly string _desiredDir;

    // Schemas owned by runtime components, not forge-db. Excluded from the diff so a
    // reconcile never plans to drop them: Hangfire (Hangfire.PostgreSql) installs and
    // migrates its own "hangfire" schema at app startup, entirely outside the desired
    // state. Without this, a reconcile against a live DB plans DROP TABLE for every
    // Hangfire table.
    private static readonly string[] ExcludedSchemas = ["hangfire"];

    private static IEnumerable<string> ExcludeArgs() =>
        ExcludedSchemas.SelectMany(s => new[] { "--exclude-schema", s });

    public PgSchemaDiffRunner(string desiredDir, string? bin = null)
    {
        _bin = bin ?? Environment.GetEnvironmentVariable("PG_SCHEMA_DIFF_BIN") ?? "pg-schema-diff";
        _desiredDir = desiredDir;
    }

    /// <summary>The migration SQL pg-schema-diff WOULD run to reconcile the target. No mutation.</summary>
    public ProcessRunner.Result Plan(string fromDsn) => ProcessRunner.Run(_bin,
    [
        "plan",
        "--from-dsn", fromDsn,
        "--to-dir", _desiredDir,
        .. ExcludeArgs(),
    ]);

    /// <summary>Apply the desired state. Caller is responsible for gates; pass the approved hazards.</summary>
    public ProcessRunner.Result Apply(string fromDsn, IReadOnlyCollection<string> allowHazards)
    {
        var args = new List<string>
        {
            "apply",
            "--from-dsn", fromDsn,
            "--to-dir", _desiredDir,
            "--skip-confirm-prompt",
        };
        args.AddRange(ExcludeArgs());
        if (allowHazards.Count > 0)
        {
            args.Add("--allow-hazards");
            args.Add(string.Join(",", allowHazards.Distinct(StringComparer.Ordinal)));
        }
        return ProcessRunner.Run(_bin, args);
    }

    public ProcessRunner.Result Version() => ProcessRunner.Run(_bin, ["version"]);

    [GeneratedRegex(@"^\s*-\s*([A-Z_]+):", RegexOptions.Multiline)]
    private static partial Regex HazardRe();

    /// <summary>Hazard tags pg-schema-diff annotated the plan with (e.g. DELETES_DATA, INDEX_BUILD).</summary>
    public static IReadOnlyList<string> Hazards(string planOutput) =>
        HazardRe().Matches(planOutput).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>True when the plan has no statements to run — target already matches desired state.</summary>
    public static bool IsInSync(string planOutput)
    {
        foreach (var raw in planOutput.Split('\n'))
        {
            var line = raw.TrimStart();
            if (line.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("ALTER ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DROP ", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}
