using Forge.Db;
using Npgsql;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// Integration coverage of <c>data/0040-work-centers-machine-burden-basis.sql</c> against a real Postgres.
/// Skips silently unless <c>FORGE_DB_TEST_DSN</c> is set (a postgres:// URL whose user has CREATEDB). It
/// creates a uniquely-named throwaway DB holding only the columns the script reads, runs the script there
/// through <see cref="DataSeedRunner.Apply"/>, and drops the DB in a finally.
/// </summary>
public class BurdenBasisBackfillDbTests
{
    private const string ScriptName = "0040-work-centers-machine-burden-basis.sql";

    private static string? Dsn => Environment.GetEnvironmentVariable("FORGE_DB_TEST_DSN");

    [Fact]
    public void Apply_SetsMachineBasisOnlyForWorkCentersWithALiveMachineAsset()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        var repo = RepoRoot();
        var baseConn = DbUrl.ToNpgsql(Dsn!);
        var admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" }.ConnectionString;
        var scratchDb = "forge_burden_backfill_" + DateTime.UtcNow.Ticks;
        var scratch = new NpgsqlConnectionStringBuilder(baseConn) { Database = scratchDb }.ConnectionString;

        Exec(admin, $"CREATE DATABASE \"{scratchDb}\"");
        var root = Path.Combine(Path.GetTempPath(), scratchDb);
        try
        {
            Exec(scratch, """
                CREATE TABLE work_centers (id integer PRIMARY KEY, asset_id integer, burden_basis smallint NOT NULL DEFAULT 0,
                    updated_at timestamptz NOT NULL DEFAULT '2000-01-01T00:00:00Z', deleted_at timestamptz);
                CREATE TABLE assets (id integer PRIMARY KEY, asset_type integer NOT NULL, work_center_id integer, deleted_at timestamptz);
                INSERT INTO assets VALUES (1, 0, NULL, NULL), (2, 1, NULL, NULL), (3, 0, 4, NULL), (4, 0, NULL, now());
                INSERT INTO work_centers (id, asset_id) VALUES (1, 1), (2, 2), (3, NULL), (4, NULL), (5, 4);
                INSERT INTO work_centers (id, asset_id, deleted_at) VALUES (6, 1, now());
                """);
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.Copy(Path.Combine(repo, "data", ScriptName), Path.Combine(root, "data", ScriptName));

            var first = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(1, first.Applied);

            Assert.Equal(1L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 1"));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 2"));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 3"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 4"));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 5"));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT burden_basis FROM work_centers WHERE id = 6"));

            Exec(scratch, "UPDATE work_centers SET updated_at = '2000-01-01T00:00:00Z'");
            Exec(scratch, File.ReadAllText(Path.Combine(repo, "data", ScriptName)));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT count(*) FROM work_centers WHERE updated_at > '2000-01-01T00:00:00Z'"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            Exec(admin, $"DROP DATABASE IF EXISTS \"{scratchDb}\" WITH (FORCE)");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

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
}
