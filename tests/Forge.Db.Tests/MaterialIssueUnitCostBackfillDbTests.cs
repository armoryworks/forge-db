using Forge.Db;
using Npgsql;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// Integration coverage of <c>data/0095-material-issue-unit-cost-from-journal.sql</c> against a real Postgres.
/// Material issues valued from the perpetual store before their relieved cost was written back kept a zero unit
/// cost while their journal debited work in process; the script sets the unit cost from that debit. Skips silently
/// unless <c>FORGE_DB_TEST_DSN</c> is set (a postgres:// URL whose user has CREATEDB). It creates a uniquely-named
/// throwaway DB holding only the columns the script reads, runs the script there through
/// <see cref="DataSeedRunner.Apply"/>, re-runs it to prove it is harmless, and drops the DB in a finally.
/// </summary>
public class MaterialIssueUnitCostBackfillDbTests
{
    private const string ScriptName = "0095-material-issue-unit-cost-from-journal.sql";

    private const string Tables = """
        CREATE TABLE acct_journal_entries (id bigint PRIMARY KEY, source_type varchar(64), source_id bigint, idempotency_key varchar(200), status varchar(20) NOT NULL);
        CREATE TABLE acct_journal_lines (id bigint PRIMARY KEY, journal_entry_id bigint NOT NULL, job_id integer, debit numeric(18,4) NOT NULL, credit numeric(18,4) NOT NULL);
        CREATE TABLE material_issues (id integer PRIMARY KEY, quantity numeric(18,4) NOT NULL, unit_cost numeric(18,4) NOT NULL, issue_type integer NOT NULL, updated_at timestamptz NOT NULL, deleted_at timestamptz);
        """;

    private const string Rows = """
        INSERT INTO material_issues VALUES
            (1, 10, 0, 0, '2000-01-01T00:00:00Z', NULL),
            (2, 4, 2.5, 0, '2000-01-01T00:00:00Z', NULL),
            (3, 10, 0, 1, '2000-01-01T00:00:00Z', NULL),
            (4, 3, 0, 0, '2000-01-01T00:00:00Z', NULL),
            (5, 6, 0, 0, '2000-01-01T00:00:00Z', '2001-01-01T00:00:00Z'),
            (6, 3, 0, 0, '2000-01-01T00:00:00Z', NULL);
        INSERT INTO acct_journal_entries VALUES
            (11, 'MaterialIssue', 1, 'Inventory:MaterialIssue:1', 'Posted'),
            (12, 'MaterialIssue', 2, 'Inventory:MaterialIssue:2', 'Posted'),
            (13, 'MaterialIssue', 3, 'Inventory:MaterialIssue:3', 'Posted'),
            (14, 'MaterialIssue', 4, 'Inventory:MaterialIssue:4', 'Draft'),
            (15, 'MaterialIssue', 5, 'Inventory:MaterialIssue:5', 'Posted'),
            (16, 'MaterialIssue', 6, 'Inventory:MaterialIssue:6', 'Reversed');
        INSERT INTO acct_journal_lines VALUES
            (101, 11, 4, 34, 0), (102, 11, NULL, 0, 34),
            (103, 12, 4, 12, 0), (104, 12, NULL, 0, 12),
            (105, 13, NULL, 34, 0), (106, 13, 4, 0, 34),
            (107, 14, 4, 99, 0), (108, 14, NULL, 0, 99),
            (109, 15, 4, 18, 0), (110, 15, NULL, 0, 18),
            (111, 16, 4, 10, 0), (112, 16, NULL, 0, 10);
        """;

    private static string? Dsn => Environment.GetEnvironmentVariable("FORGE_DB_TEST_DSN");

    [Fact]
    public void Apply_SetsTheUnitCostOfZeroCostIssuesFromTheirWipDebit_AndLeavesTheRestAlone()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        var repo = RepoRoot();
        var baseConn = DbUrl.ToNpgsql(Dsn!);
        var admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" }.ConnectionString;
        var scratchDb = "forge_issue_cost_backfill_" + DateTime.UtcNow.Ticks;
        var scratch = new NpgsqlConnectionStringBuilder(baseConn) { Database = scratchDb }.ConnectionString;

        Exec(admin, $"CREATE DATABASE \"{scratchDb}\"");
        var root = Path.Combine(Path.GetTempPath(), scratchDb);
        try
        {
            Exec(scratch, Tables);
            Exec(scratch, Rows);
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.Copy(Path.Combine(repo, "data", ScriptName), Path.Combine(root, "data", ScriptName));

            var first = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(1, first.Applied);

            Assert.Equal("3.4000", Cost(scratch, 1));
            Assert.Equal("2.5000", Cost(scratch, 2));
            Assert.Equal("0.0000", Cost(scratch, 3));
            Assert.Equal("0.0000", Cost(scratch, 4));
            Assert.Equal("0.0000", Cost(scratch, 5));
            Assert.Equal("0.0000", Cost(scratch, 6));

            Exec(scratch, "UPDATE material_issues SET updated_at = '2000-01-01T00:00:00Z'");
            Exec(scratch, File.ReadAllText(Path.Combine(repo, "data", ScriptName)));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT count(*) FROM material_issues WHERE updated_at > '2000-01-01T00:00:00Z'"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            Exec(admin, $"DROP DATABASE IF EXISTS \"{scratchDb}\" WITH (FORCE)");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Cost(string conn, int id)
        => ScalarString(conn, $"SELECT unit_cost::text FROM material_issues WHERE id = {id}");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", ScriptName)))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException($"data/{ScriptName} not found above the test output.");
    }

    private static void Exec(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(cmd.ExecuteScalar()!);
    }

    private static string ScalarString(string conn, string sql)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToString(cmd.ExecuteScalar()!)!;
    }
}
