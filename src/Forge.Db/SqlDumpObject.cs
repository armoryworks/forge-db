namespace Forge.Db;

/// <summary>
/// One object parsed out of a <c>pg_dump --schema-only</c> file, keyed by the structured
/// <c>-- Name: …; Type: …</c> header pg_dump emits before every statement block.
/// </summary>
/// <param name="Name">The header Name field, e.g. <c>vector</c>, <c>acct_journal_lines</c>,
/// <c>acct_journal_lines pk_acct_journal_lines</c> (table + object for child objects).</param>
/// <param name="Type">The header Type field, e.g. <c>TABLE</c>, <c>FK CONSTRAINT</c>, <c>INDEX</c>.</param>
/// <param name="Sql">The object's SQL statement(s), cleaned of pg_dump preamble/psql meta lines.</param>
public sealed record SqlDumpObject(string Name, string Type, string Sql);
