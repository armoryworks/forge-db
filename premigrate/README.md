# premigrate/ — applied-once scripts that run BEFORE the schema reconcile (INPUT)

The escape hatch for changes pg-schema-diff cannot express.

## Why this exists

pg-schema-diff compares **states**, not intent. It cannot see a rename: it sees a table that is gone
and a table that is new, and plans `DROP` + `CREATE`. On a populated database that destroys every
row.

`data/` cannot cover it either — those scripts run *after* the reconcile, by which point the old
table is already gone.

So renames, and anything else that must reshape the database before the diff is even computed, live
here.

## Ordering

`forge-db apply` runs, in order:

1. **`premigrate/`** ← this directory
2. schema reconcile (pg-schema-diff)
3. `data/` then `seed/`

Note that step 1 runs before the plan is *computed*, not merely before it is applied. The plan is
derived from live DB state, so a rename applied after planning would be reconciling against a shape
that no longer exists.

## Authoring convention

Same contract as [`../data/README.md`](../data/README.md):

- **One concern per file, zero-padded numeric prefix** (`0010-…​.sql`). Leave gaps of 10.
- **Applied-once**, tracked in `forge_db.data_migration_log` — the same ledger `data/` and `seed/`
  use, so a name can never be applied twice across phases.
- **Idempotent anyway.** Use `ALTER TABLE IF EXISTS`, `ADD COLUMN IF NOT EXISTS`, and so on. A script
  must be safe against a database already at the target shape, because that is exactly what the
  second deploy looks like.
- **Never edit an applied script.** The harness warns on a changed checksum. Add a new numbered
  script for the delta.
- **Each script runs in its own transaction.** A failure rolls it back and stops the run before any
  schema change is attempted.

## What does NOT belong here

- Column additions, drops, type changes, index changes — pg-schema-diff handles all of those from
  `schema/`. Putting them here means the desired state and the database drift apart.
- Data backfills → `data/`.
- Reference rows → `seed/`.

If pg-schema-diff can express it, let it.

## A note on `plan`

`plan` is a pure read and does **not** run these scripts. When any are pending it warns, because the
plan it prints is the one that would run *without* them — including the `DELETES_DATA` hazard the
rename exists to avoid. Do not reach for `--allow-destructive` on the strength of a plan that has a
pending pre-migrate warning above it.
