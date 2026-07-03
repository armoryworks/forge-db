using Forge.Db;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// DB-free coverage of the data/seed engine's discovery, ordering, applied-once, and checksum logic
/// (DESIGN §6.1). The Apply-against-Postgres path is exercised manually per the e2e steps in
/// data/README.md — these tests need no live database, matching the rest of the suite.
/// </summary>
public class DataSeedRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "forge-db-seed-" + Guid.NewGuid().ToString("N"));

    private void Write(string kind, string file, string sql = "SELECT 1;")
    {
        var dir = Path.Combine(_root, kind);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), sql);
    }

    [Fact]
    public void Discover_OrdersDataBeforeSeed_AndNumericallyWithinEach()
    {
        Write("seed", "0020-carriers.sql");
        Write("seed", "0010-reference-data.sql");
        Write("data", "0010-role-template-teardown.sql");

        var names = DataSeedRunner.Discover(_root).Select(s => s.Name).ToArray();

        Assert.Equal(
            ["data/0010-role-template-teardown.sql", "seed/0010-reference-data.sql", "seed/0020-carriers.sql"],
            names);
    }

    [Fact]
    public void Discover_EmptyOrMissingDirs_ReturnsNothing()
    {
        Assert.Empty(DataSeedRunner.Discover(_root));                 // neither dir exists
        Directory.CreateDirectory(Path.Combine(_root, "seed"));       // present but empty
        Assert.Empty(DataSeedRunner.Discover(_root));
    }

    [Fact]
    public void Discover_IgnoresNonSqlFiles()
    {
        Write("seed", "0010-groups.sql");
        Write("seed", "README.md", "# not a script");
        Assert.Single(DataSeedRunner.Discover(_root));
    }

    [Fact]
    public void Pending_ExcludesLedgeredScripts_PreservingOrder()
    {
        Write("seed", "0010-a.sql");
        Write("seed", "0020-b.sql");
        Write("seed", "0030-c.sql");
        var all = DataSeedRunner.Discover(_root);

        var applied = new HashSet<string> { "seed/0010-a.sql", "seed/0030-c.sql" };
        var pending = DataSeedRunner.Pending(all, applied).Select(s => s.Name).ToArray();

        Assert.Equal(["seed/0020-b.sql"], pending);
    }

    [Fact]
    public void Pending_AllApplied_IsEmpty()
    {
        Write("seed", "0010-a.sql");
        var all = DataSeedRunner.Discover(_root);
        Assert.Empty(DataSeedRunner.Pending(all, new HashSet<string> { "seed/0010-a.sql" }));
    }

    [Fact]
    public void Checksum_IsStable_AndNewlineAgnostic()
    {
        Assert.Equal(DataSeedRunner.Checksum("INSERT INTO x VALUES (1);\n"),
                     DataSeedRunner.Checksum("INSERT INTO x VALUES (1);\r\n"));
        Assert.NotEqual(DataSeedRunner.Checksum("INSERT INTO x VALUES (1);"),
                        DataSeedRunner.Checksum("INSERT INTO x VALUES (2);"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
