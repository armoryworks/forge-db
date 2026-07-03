using Forge.Db;
using Npgsql;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// Integration coverage of <see cref="DataSeedRunner.Apply"/> against a real Postgres. Skips silently
/// unless <c>FORGE_DB_TEST_DSN</c> is set (a postgres:// URL whose user has CREATEDB) — matching the
/// suite's default DB-free posture. It never touches the target database: it creates a uniquely-named
/// throwaway DB, runs entirely inside it, and drops it in a finally.
/// </summary>
public class DataSeedRunnerDbTests
{
    private static string? Dsn => Environment.GetEnvironmentVariable("FORGE_DB_TEST_DSN");

    [Fact]
    public void Apply_AppliesOnce_RecordsLedger_IsIdempotent_AndGatesNonDev()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return; // no DB configured — skip (see class summary)

        var baseConn = DbUrl.ToNpgsql(Dsn!);
        var admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" }.ConnectionString;
        var scratchDb = "forge_seed_e2e_" + DateTime.UtcNow.Ticks;
        var scratch = new NpgsqlConnectionStringBuilder(baseConn) { Database = scratchDb }.ConnectionString;

        Exec(admin, $"CREATE DATABASE \"{scratchDb}\"");
        var root = Path.Combine(Path.GetTempPath(), scratchDb);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "seed"));
            File.WriteAllText(Path.Combine(root, "seed", "0010-widgets.sql"), """
                CREATE TABLE IF NOT EXISTS widgets (code text PRIMARY KEY, label text NOT NULL);
                INSERT INTO widgets (code, label) VALUES ('A', 'Anvil')
                    ON CONFLICT (code) DO NOTHING;
                """);

            // Non-dev + unconfirmed → blocked, and NOTHING is mutated (widgets never created).
            var blocked = DataSeedRunner.Apply(root, scratch, allowMutation: false);
            Assert.True(blocked.Blocked);
            Assert.Equal(0, blocked.Applied);
            Assert.False(TableExists(scratch, "public", "widgets"));

            // Confirmed → applies once, records the ledger, creates the row.
            var first = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(1, first.Applied);
            Assert.False(first.Blocked);
            Assert.Equal(1L, ScalarLong(scratch, "SELECT count(*) FROM widgets"));
            Assert.Equal(1L, ScalarLong(scratch, "SELECT count(*) FROM forge_db.data_migration_log WHERE script_name = 'seed/0010-widgets.sql'"));

            // Re-run → no-op: the ledger skips it, no duplicate row.
            var second = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(0, second.Applied);
            Assert.Equal(1, second.AlreadyApplied);
            Assert.Equal(1L, ScalarLong(scratch, "SELECT count(*) FROM widgets"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            Exec(admin, $"DROP DATABASE IF EXISTS \"{scratchDb}\" WITH (FORCE)");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
        return (long)cmd.ExecuteScalar()!;
    }

    private static bool TableExists(string conn, string schema, string table) =>
        ScalarLong(conn,
            $"SELECT count(*) FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{table}'") > 0;
}
