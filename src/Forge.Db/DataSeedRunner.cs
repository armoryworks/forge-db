using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Forge.Db;

/// <summary>
/// Applies the ordered, applied-once <c>data/</c> then <c>seed/</c> scripts (docs/DESIGN §6.1) after
/// the schema apply. pg-schema-diff generates DDL, never data, so this is the one change-based area:
/// each script runs exactly once (recorded in <c>forge_db.data_migration_log</c>) and is authored to
/// be safe if re-run anyway (WHERE/NOT EXISTS / ON CONFLICT guards) as belt-and-suspenders.
///
/// <para><b>Ordering:</b> scripts sort lexicographically by filename within each directory, so the
/// convention is a zero-padded numeric prefix (<c>0010-…​.sql</c>, <c>0020-…​.sql</c>). <c>data/</c>
/// runs before <c>seed/</c>.</para>
///
/// <para><b>The ledger lives in a harness-owned <c>forge_db</c> schema</b>, not in <c>schema/</c>, and
/// that schema is excluded from the pg-schema-diff reconcile (see
/// <see cref="PgSchemaDiffRunner"/>) — so it never reads as desired-state drift and never pollutes the
/// EF drift-check. This runner creates it defensively (<c>CREATE … IF NOT EXISTS</c>).</para>
/// </summary>
public sealed class DataSeedRunner
{
    public const string LedgerSchema = "forge_db";
    public const string LedgerTable = "forge_db.data_migration_log";

    /// <summary>One authored script. <see cref="Name"/> (e.g. <c>seed/0010-reference-data.sql</c>) is
    /// the stable ledger key — relative, forward-slashed, unique across data/ and seed/.</summary>
    public sealed record Script(string Kind, string Name, string Path);

    public sealed record Result(int Total, int AlreadyApplied, int Applied, bool Blocked, string? BlockedReason)
    {
        public static readonly Result Empty = new(0, 0, 0, false, null);
    }

    // ── Pure, DB-free discovery/ordering (unit-tested without Postgres) ──────────────────────────

    /// <summary>All data/ then seed/ scripts, ordered lexicographically by filename within each dir.</summary>
    public static IReadOnlyList<Script> Discover(string repoRoot)
    {
        var scripts = new List<Script>();
        scripts.AddRange(Enumerate(repoRoot, SchemaLayout.DataDir));
        scripts.AddRange(Enumerate(repoRoot, SchemaLayout.SeedDir));
        return scripts;
    }

    private static IEnumerable<Script> Enumerate(string repoRoot, string kind)
    {
        var dir = Path.Combine(repoRoot, kind);
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.EnumerateFiles(dir, "*.sql", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            yield return new Script(kind, $"{kind}/{Path.GetFileName(f)}", f);
    }

    /// <summary>Scripts not yet in the ledger, preserving discovery order.</summary>
    public static IReadOnlyList<Script> Pending(IReadOnlyList<Script> all, ISet<string> applied) =>
        all.Where(s => !applied.Contains(s.Name)).ToList();

    public static string Checksum(string sql)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n")));
        return Convert.ToHexStringLower(hash);
    }

    // ── DB execution ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Run every pending data/ + seed/ script once, each in its own transaction, recording it in the
    /// ledger. <paramref name="allowMutation"/> false + pending &gt; 0 returns a Blocked result WITHOUT
    /// touching the DB (the caller's gate for non-dev targets). Idempotent: a second run is a no-op.
    /// </summary>
    public static Result Apply(string repoRoot, string connString, bool allowMutation)
    {
        var all = Discover(repoRoot);
        if (all.Count == 0) return Result.Empty;

        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        EnsureLedger(conn);

        var applied = LoadApplied(conn);
        WarnOnChangedScripts(all, applied);
        var pending = Pending(all, new HashSet<string>(applied.Keys, StringComparer.Ordinal));
        var alreadyApplied = all.Count - pending.Count;

        if (pending.Count > 0 && !allowMutation)
            return new Result(all.Count, alreadyApplied, 0, true,
                $"{pending.Count} pending data/seed script(s) but target is non-dev and not confirmed " +
                "(pass --yes --backup-taken).");

        foreach (var s in pending)
        {
            var sql = File.ReadAllText(s.Path);
            using var tx = conn.BeginTransaction();
            using (var cmd = new NpgsqlCommand(sql, conn, tx))
                cmd.ExecuteNonQuery();
            using (var log = new NpgsqlCommand(
                $"INSERT INTO {LedgerTable} (script_name, checksum) VALUES (@n, @c)", conn, tx))
            {
                log.Parameters.AddWithValue("n", s.Name);
                log.Parameters.AddWithValue("c", Checksum(sql));
                log.ExecuteNonQuery();
            }
            tx.Commit();
            Console.WriteLine($"[apply]   ran {s.Name}");
        }

        return new Result(all.Count, alreadyApplied, pending.Count, false, null);
    }

    private static void EnsureLedger(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand($"""
            CREATE SCHEMA IF NOT EXISTS {LedgerSchema};
            CREATE TABLE IF NOT EXISTS {LedgerTable} (
                script_name text PRIMARY KEY,
                applied_at  timestamptz NOT NULL DEFAULT now(),
                checksum    text NOT NULL
            );
            """, conn);
        cmd.ExecuteNonQuery();
    }

    private static Dictionary<string, string> LoadApplied(NpgsqlConnection conn)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = new NpgsqlCommand($"SELECT script_name, checksum FROM {LedgerTable}", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) map[r.GetString(0)] = r.GetString(1);
        return map;
    }

    /// <summary>
    /// Applied-once means an edited script will NOT re-run; a changed checksum almost always signals a
    /// mistake (edit an applied script instead of adding a new one). Warn loudly; do not fail.
    /// </summary>
    private static void WarnOnChangedScripts(IReadOnlyList<Script> all, IReadOnlyDictionary<string, string> applied)
    {
        foreach (var s in all)
            if (applied.TryGetValue(s.Name, out var was) && was != Checksum(File.ReadAllText(s.Path)))
                Console.Error.WriteLine(
                    $"[apply] WARNING: {s.Name} changed since it was applied — applied-once scripts do " +
                    "not re-run. Add a NEW script for the delta instead of editing this one.");
    }
}
