using Forge.Db;
using Npgsql;
using Xunit;

namespace Forge.Db.Tests;

/// <summary>
/// Integration coverage of <c>data/0030-clear-invalid-machine-terminology.sql</c> against a real Postgres.
/// Skips silently unless <c>FORGE_DB_TEST_DSN</c> is set (a postgres:// URL whose user has CREATEDB). It
/// creates a uniquely-named throwaway DB holding only <c>terminology_entries</c>, runs the script there
/// through <see cref="DataSeedRunner.Apply"/>, and drops the DB in a finally.
/// </summary>
public class TerminologyBackfillDbTests
{
    private const string ScriptName = "0030-clear-invalid-machine-terminology.sql";
    private const string Machine = "machine-translation";

    private static string? Dsn => Environment.GetEnvironmentVariable("FORGE_DB_TEST_DSN");

    [Fact]
    public void Apply_ClearsWholeTermForInvalidMachineRows_AndLeavesEverythingElse()
    {
        if (string.IsNullOrWhiteSpace(Dsn)) return;

        var repo = RepoRoot();
        var baseConn = DbUrl.ToNpgsql(Dsn!);
        var admin = new NpgsqlConnectionStringBuilder(baseConn) { Database = "postgres" }.ConnectionString;
        var scratchDb = "forge_term_backfill_" + DateTime.UtcNow.Ticks;
        var scratch = new NpgsqlConnectionStringBuilder(baseConn) { Database = scratchDb }.ConnectionString;

        Exec(admin, $"CREATE DATABASE \"{scratchDb}\"");
        var root = Path.Combine(Path.GetTempPath(), scratchDb);
        try
        {
            Exec(scratch, File.ReadAllText(Path.Combine(repo, "schema", "tables", "terminology_entries.sql")));
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.Copy(Path.Combine(repo, "data", ScriptName), Path.Combine(root, "data", ScriptName));

            Insert(scratch, "entity_job:es", "Canned assistant answer. This response is fixed for testing and is not a noun.", true, Machine);
            Insert(scratch, "label_jobs:es", "Trabajos", true, Machine);
            Insert(scratch, "gender_job:es", "m", true, Machine);
            Insert(scratch, "entity_customer:es", "Cliente", true, Machine);
            Insert(scratch, "label_customers:es", "Clientes", true, Machine);
            Insert(scratch, "entity_part:es", "Pieza", true, Machine);
            Insert(scratch, "label_parts:es", "Nota: piezas", true, Machine);
            Insert(scratch, "gender_part:es", "f", true, null);
            Insert(scratch, "entity_vendor:es", "Proveedor: el que vende", true, null);
            Insert(scratch, "label_quotes:es", new string('x', 61), true, Machine, deleted: true);
            Insert(scratch, "entity_lead:es", new string('x', 61), false, null);

            var first = DataSeedRunner.Apply(root, scratch, allowMutation: true);
            Assert.Equal(1, first.Applied);

            Assert.Equal(("", true, Machine), Row(scratch, "entity_job:es"));
            Assert.Equal(("", true, Machine), Row(scratch, "label_jobs:es"));
            Assert.Equal(("", true, Machine), Row(scratch, "gender_job:es"));
            Assert.Equal(("Cliente", true, Machine), Row(scratch, "entity_customer:es"));
            Assert.Equal(("Clientes", true, Machine), Row(scratch, "label_customers:es"));
            Assert.Equal(("", true, Machine), Row(scratch, "entity_part:es"));
            Assert.Equal(("", true, Machine), Row(scratch, "label_parts:es"));
            Assert.Equal(("f", true, (string?)null), Row(scratch, "gender_part:es"));
            Assert.Equal(("Proveedor: el que vende", true, (string?)null), Row(scratch, "entity_vendor:es"));
            Assert.Equal((new string('x', 61), true, Machine), Row(scratch, "label_quotes:es"));
            Assert.Equal((new string('x', 61), false, (string?)null), Row(scratch, "entity_lead:es"));

            Exec(scratch, "UPDATE terminology_entries SET updated_at = '2000-01-01T00:00:00Z'");
            Exec(scratch, File.ReadAllText(Path.Combine(repo, "data", ScriptName)));
            Assert.Equal(0L, ScalarLong(scratch, "SELECT count(*) FROM terminology_entries WHERE updated_at > '2000-01-01T00:00:00Z'"));
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

    private static void Insert(string conn, string key, string label, bool adminEdited, string? source, bool deleted = false)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO terminology_entries (key, label, is_admin_edited, source_preset_id, created_at, updated_at, deleted_at)
            VALUES (@key, @label, @adminEdited, @source, now(), now(), CASE WHEN @deleted THEN now() END)
            """, c);
        cmd.Parameters.AddWithValue("key", key);
        cmd.Parameters.AddWithValue("label", label);
        cmd.Parameters.AddWithValue("adminEdited", adminEdited);
        cmd.Parameters.AddWithValue("source", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("deleted", deleted);
        cmd.ExecuteNonQuery();
    }

    private static (string Label, bool AdminEdited, string? Source) Row(string conn, string key)
    {
        using var c = new NpgsqlConnection(conn);
        c.Open();
        using var cmd = new NpgsqlCommand(
            "SELECT label, is_admin_edited, source_preset_id FROM terminology_entries WHERE key = @key", c);
        cmd.Parameters.AddWithValue("key", key);
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2));
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
}
