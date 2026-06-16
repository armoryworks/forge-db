namespace Forge.Db;

/// <summary>
/// Orchestrates the Atlas CLI (docs/DESIGN.md §4). Atlas owns diff/apply correctness; this type
/// only decides what to invoke and against which URLs. Atlas is resolved from PATH (we ship the
/// binary in ~/.local/bin); override with the <c>ATLAS_BIN</c> env var.
/// </summary>
public sealed class AtlasRunner
{
    private readonly string _bin;
    private readonly string _schemaUrl;   // file://<assembled desired-state .sql>
    private readonly string _devUrl;      // a throwaway pgvector DB atlas uses to normalize desired state

    // Atlas's --exclude selector must be SCHEMA-qualified (bare/​wildcard names don't match). EF's
    // migration-history tables are owned by EF, never part of forge-db's desired state.
    private const string EfExclude =
        "public." + SchemaLayout.EfHistoryTable + ",public." + SchemaLayout.EfHistoryTable + "_pre_squash";

    public AtlasRunner(string desiredSqlPath, string devUrl, string? bin = null)
    {
        _bin = bin ?? Environment.GetEnvironmentVariable("ATLAS_BIN") ?? "atlas";
        _schemaUrl = "file://" + desiredSqlPath;
        _devUrl = devUrl;
    }

    /// <summary>The SQL Atlas would run to bring <paramref name="targetDbUrl"/> to desired state. No mutation.</summary>
    public ProcessRunner.Result Plan(string targetDbUrl) => ProcessRunner.Run(_bin,
    [
        "schema", "apply",
        "--url", targetDbUrl,
        "--to", _schemaUrl,
        "--dev-url", _devUrl,
        "--exclude", EfExclude,
        "--dry-run",
    ]);

    /// <summary>Apply desired state to <paramref name="targetDbUrl"/>. Caller is responsible for gates.</summary>
    public ProcessRunner.Result Apply(string targetDbUrl, bool autoApprove) => ProcessRunner.Run(_bin,
    [
        "schema", "apply",
        "--url", targetDbUrl,
        "--to", _schemaUrl,
        "--dev-url", _devUrl,
        "--exclude", EfExclude,
        .. autoApprove ? new[] { "--auto-approve" } : Array.Empty<string>(),
    ]);

    /// <summary>Diff live vs desired. Empty diff (exit 0, no statements) == in sync — for CI verify.</summary>
    public ProcessRunner.Result Diff(string targetDbUrl) => ProcessRunner.Run(_bin,
    [
        "schema", "diff",
        "--from", targetDbUrl,
        "--to", _schemaUrl,
        "--dev-url", _devUrl,
        "--exclude", EfExclude,
    ]);

    public ProcessRunner.Result Version() => ProcessRunner.Run(_bin, ["version"]);
}
