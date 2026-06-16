using Forge.Db;
using Xunit;

namespace Forge.Db.Tests;

public class HarnessUnitTests
{
    [Fact]
    public void DestructiveStatements_FlagsDropsAndColumnRemovals()
    {
        const string plan = """
            CREATE TABLE foo (id int);
            DROP TABLE bar;
            ALTER TABLE baz DROP COLUMN qux;
            CREATE INDEX ix ON foo (id);
            """;
        var destructive = DeployGates.DestructiveStatements(plan);
        Assert.Equal(2, destructive.Count); // DROP TABLE + ALTER ... DROP COLUMN; the CREATEs are safe.
    }

    [Fact]
    public void Gates_BlockDestructiveWithoutFlag_AllowWithFlag()
    {
        const string plan = "DROP TABLE bar;";
        Assert.False(DeployGates.Evaluate(plan, isDev: true, confirmed: true, backupTaken: true, allowDestructive: false).Allowed);
        Assert.True(DeployGates.Evaluate(plan, isDev: true, confirmed: true, backupTaken: true, allowDestructive: true).Allowed);
    }

    [Fact]
    public void Gates_NonDevRequiresConfirmAndBackup()
    {
        const string plan = "CREATE TABLE foo (id int);";
        Assert.False(DeployGates.Evaluate(plan, isDev: false, confirmed: false, backupTaken: false, allowDestructive: false).Allowed);
        Assert.False(DeployGates.Evaluate(plan, isDev: false, confirmed: true, backupTaken: false, allowDestructive: false).Allowed);
        Assert.True(DeployGates.Evaluate(plan, isDev: false, confirmed: true, backupTaken: true, allowDestructive: false).Allowed);
    }

    [Fact]
    public void DbUrl_ConvertsAtlasUrlToNpgsql()
    {
        var conn = DbUrl.ToNpgsql("postgres://postgres:scratch@127.0.0.1:55432/forge?sslmode=disable");
        Assert.Contains("Host=127.0.0.1", conn);
        Assert.Contains("Port=55432", conn);
        Assert.Contains("Database=forge", conn);
        Assert.Contains("Username=postgres", conn);
        Assert.Contains("Password=scratch", conn);
        Assert.Contains("SSL Mode=Disable", conn);
    }
}
