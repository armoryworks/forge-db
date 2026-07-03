# data/ — ordered backfill scripts (INPUT)

Hand-written, explicitly-idempotent data backfills, run in coordination with a schema `apply`
(docs/DESIGN.md §6.1). pg-schema-diff generates **DDL, never data**, so coupled changes — add a `NOT NULL`
column → backfill existing rows → enforce the constraint — live here.

This is the **one** place forge-db is change-based rather than declarative: each script runs once
and is written to be safe if re-run anyway (`WHERE … NOT EXISTS` guards).

Distinct from `history/` (an OUTPUT you never edit). Empty until the first coupled
schema-change-plus-backfill lands.

## Authoring convention (applies to `data/` and `seed/`)

Both directories are applied by `DataSeedRunner` during `forge-db apply`, **after** the schema
reconcile. `data/` runs before `seed/`.

- **One concern per file, zero-padded numeric prefix:** `0010-role-template-teardown.sql`,
  `0020-….sql`. Files run in **lexicographic order within each directory** — the prefix *is* the
  ordering, so leave gaps (increment by 10) to allow inserts.
- **Applied-once, tracked in a ledger.** The harness records each applied script in
  `forge_db.data_migration_log` (`script_name` = the repo-relative name, e.g.
  `data/0010-role-template-teardown.sql`). Re-deploys skip anything already in the ledger. The
  `forge_db` schema is harness-owned bookkeeping and is **excluded from the pg-schema-diff reconcile**
  (like `hangfire`), so it never reads as desired-state drift.
- **Never edit an applied script.** Applied-once means an edit does **not** re-run — the harness
  compares checksums and prints a loud warning if an already-applied script changed. To change
  applied data, add a **new** numbered script for the delta.
- **Idempotent anyway (belt-and-suspenders).** On top of the ledger, write each script so a second
  run is harmless: `INSERT … ON CONFLICT DO NOTHING`, `WHERE NOT EXISTS`, `CREATE … IF NOT EXISTS`.
- **Each script runs in its own transaction.** A failure rolls back that script and stops the run;
  scripts that already succeeded stay applied and ledgered.
- **Coupled schema+data changes** interleave as two desired-state steps around a backfill: add nullable
  column (schema) → backfill (`data/`) → enforce `NOT NULL` (schema), across separate applies.

On non-dev targets the data/seed phase requires `--yes --backup-taken`, mirroring the schema gate.
