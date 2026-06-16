namespace Forge.Db;

/// <summary>
/// Converts a single Atlas-style Postgres URL (<c>postgres://user:pass@host:port/db?sslmode=…</c>)
/// into an Npgsql connection string, so callers pass one <c>--db</c> value that both Atlas and the
/// trigger/function verifier (Npgsql) can use.
/// </summary>
public static class DbUrl
{
    public static string ToNpgsql(string url)
    {
        var u = new Uri(url);
        var userInfo = u.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var db = u.AbsolutePath.Trim('/');
        var port = u.Port > 0 ? u.Port : 5432;

        var sb = new System.Text.StringBuilder();
        sb.Append($"Host={u.Host};Port={port};Database={db};Username={user};Password={pass}");

        var ssl = QueryValue(u.Query, "sslmode");
        if (!string.IsNullOrEmpty(ssl)) sb.Append($";SSL Mode={MapSsl(ssl)}");
        return sb.ToString();
    }

    private static string? QueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : "");
        }
        return null;
    }

    private static string MapSsl(string sslmode) => sslmode.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Allow",
        "prefer" => "Prefer",
        "require" => "Require",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => "Prefer",
    };
}
