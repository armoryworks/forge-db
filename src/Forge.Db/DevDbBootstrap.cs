using Npgsql;

namespace Forge.Db;

/// <summary>
/// Atlas loads the desired state by executing it against its dev DB, but the Atlas free tier won't
/// process <c>CREATE EXTENSION</c>. So before any Atlas run we pre-seed the dev DB with every
/// extension the schema declares (idempotent), letting Atlas create the <c>vector</c>-typed tables
/// without ever issuing a CREATE EXTENSION itself.
/// </summary>
public static class DevDbBootstrap
{
    public static void EnsureExtensions(string devUrl, string repoRoot)
    {
        var names = DesiredExtensionNames(repoRoot);
        if (names.Count == 0) return;

        using var conn = new NpgsqlConnection(DbUrl.ToNpgsql(devUrl));
        conn.Open();
        foreach (var name in names)
        {
            using var cmd = new NpgsqlCommand($"CREATE EXTENSION IF NOT EXISTS \"{name}\"", conn);
            cmd.ExecuteNonQuery();
        }
    }

    public static IReadOnlyList<string> DesiredExtensionNames(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, SchemaLayout.Extensions);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.sql").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList()
            : [];
    }
}
