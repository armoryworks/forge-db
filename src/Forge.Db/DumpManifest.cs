using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Db;

/// <summary>
/// The receipt written next to a data dump (<c>manifest.json</c>) and read back by import. It is the
/// contract between the two: which tables were dumped, with which columns (import COPYs exactly the
/// intersection of these and the target's columns, so modest schema evolution between dump and
/// import doesn't break the load), how many rows, and a fingerprint of the desired-state schema at
/// dump time (import warns on mismatch — the column intersection usually absorbs the drift).
/// </summary>
public sealed record DumpManifest(
    string DumpedAtUtc,
    string SourceHost,
    string SourceDatabase,
    string? SchemaFingerprint,
    IReadOnlyList<DumpManifest.Table> Tables)
{
    /// <summary>One dumped table. <see cref="File"/> is relative to the dump directory.</summary>
    public sealed record Table(
        string Schema,
        string Name,
        IReadOnlyList<string> Columns,
        long Rows,
        long Bytes,
        string Sha256,
        string File)
    {
        [JsonIgnore] public string Qualified => $"{Schema}.{Name}";
    }

    public const string FileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public void Save(string dumpDir) =>
        System.IO.File.WriteAllText(Path.Combine(dumpDir, FileName), JsonSerializer.Serialize(this, JsonOpts));

    public static DumpManifest Load(string dumpDir)
    {
        var path = Path.Combine(dumpDir, FileName);
        if (!System.IO.File.Exists(path))
            throw new FileNotFoundException($"not a forge-db dump directory (no {FileName}): {dumpDir}");
        return JsonSerializer.Deserialize<DumpManifest>(System.IO.File.ReadAllText(path), JsonOpts)
               ?? throw new InvalidDataException($"could not parse {path}");
    }
}
