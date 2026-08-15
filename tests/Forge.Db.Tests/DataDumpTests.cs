using Forge.Db;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>DB-free coverage of the dump/import building blocks: exclude-glob semantics, COPY text
/// row splitting (the column-projection path), and the manifest round-trip.</summary>
public class DataDumpTests
{
    // ── TableGlob ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("audit_*", "public", "audit_events", true)]
    [InlineData("audit_*", "public", "accounts", false)]
    [InlineData("public.audit_*", "public", "audit_events", true)]
    [InlineData("public.audit_*", "billing", "audit_events", false)]
    [InlineData("*_log", "any_schema", "request_log", true)]
    [InlineData("*.request_log", "billing", "request_log", true)]
    [InlineData("request_lo?", "public", "request_log", true)]
    [InlineData("AUDIT_*", "public", "audit_events", true)] // case-insensitive
    public void Glob_MatchesQualifiedAndBareNames(string pattern, string schema, string table, bool expected) =>
        Assert.Equal(expected, TableGlob.Matches(pattern, schema, table));

    [Fact]
    public void Glob_ParsesCsvAndMatchesAny()
    {
        var patterns = TableGlob.Parse(" audit_*, *_log ,tmp_scratch ");
        Assert.Equal(3, patterns.Count);
        Assert.True(TableGlob.MatchesAny(patterns, "public", "tmp_scratch"));
        Assert.True(TableGlob.MatchesAny(patterns, "billing", "request_log"));
        Assert.False(TableGlob.MatchesAny(patterns, "public", "accounts"));
        Assert.Empty(TableGlob.Parse(null));
        Assert.Empty(TableGlob.Parse("  "));
    }

    // ── COPY text row splitting (import's dropped-column projection) ─────────────────────────────

    [Fact]
    public void SplitCopyLine_SplitsOnTabs_PreservingEscapedTabsInsideValues()
    {
        // COPY text escapes an in-value tab as the two characters \t — never a literal tab.
        var fields = DataImporter.SplitCopyLine("1\thas \\t inside\t\\N", 3);
        Assert.Equal(["1", @"has \t inside", @"\N"], fields);
    }

    [Fact]
    public void SplitCopyLine_LastFieldKeepsTrailingContent()
    {
        var fields = DataImporter.SplitCopyLine("a\tb\tc\td", 3); // fewer expected than tabs → tail stays whole
        Assert.Equal(["a", "b", "c\td"], fields);
    }

    // ── Manifest round-trip ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Manifest_RoundTripsThroughJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-db-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new DumpManifest(
                DumpedAtUtc: "2026-08-15T00:00:00Z",
                SourceHost: "db.example",
                SourceDatabase: "forge",
                SchemaFingerprint: "abc123",
                Tables:
                [
                    new DumpManifest.Table("public", "accounts", ["id", "name"], 42, 1024, "deadbeef",
                        "tables/public.accounts.copy"),
                ]);
            manifest.Save(dir);

            var loaded = DumpManifest.Load(dir);
            Assert.Equal(manifest.SchemaFingerprint, loaded.SchemaFingerprint);
            var t = Assert.Single(loaded.Tables);
            Assert.Equal("public.accounts", t.Qualified);
            Assert.Equal(["id", "name"], t.Columns);
            Assert.Equal(42L, t.Rows);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Manifest_Load_ThrowsClearErrorOnNonDumpDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-db-notadump-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<FileNotFoundException>(() => DumpManifest.Load(dir));
            Assert.Contains("not a forge-db dump directory", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
