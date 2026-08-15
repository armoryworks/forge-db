using System.Text.RegularExpressions;

namespace Forge.Db;

/// <summary>
/// Shell-style glob matching over schema-qualified table names (<c>schema.table</c>), used by
/// <c>import --exclude</c>. <c>*</c> matches any run of characters (including the dot separator, so
/// <c>*_log</c> excludes a <c>_log</c> table in any schema); <c>?</c> matches one character. A
/// pattern with no dot is matched against the bare table name as well, so <c>audit_*</c> reads the
/// way an operator expects without them having to write <c>*.audit_*</c>.
/// </summary>
public static class TableGlob
{
    public static IReadOnlyList<string> Parse(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Matches(string pattern, string schema, string table)
    {
        var re = ToRegex(pattern);
        return re.IsMatch($"{schema}.{table}")
               || (!pattern.Contains('.') && re.IsMatch(table));
    }

    public static bool MatchesAny(IReadOnlyList<string> patterns, string schema, string table) =>
        patterns.Any(p => Matches(p, schema, table));

    private static Regex ToRegex(string glob) =>
        new("^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
