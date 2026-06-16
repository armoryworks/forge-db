# data/ — ordered backfill scripts (INPUT)

Hand-written, explicitly-idempotent data backfills, run in coordination with a schema `apply`
(docs/DESIGN.md §6.1). pg-schema-diff generates **DDL, never data**, so coupled changes — add a `NOT NULL`
column → backfill existing rows → enforce the constraint — live here.

This is the **one** place forge-db is change-based rather than declarative: each script runs once
(the harness records applied scripts in a `data_migration_log` table) and is written to be safe if
re-run anyway (`WHERE … NOT EXISTS` guards).

Distinct from `history/` (an OUTPUT you never edit). Empty until the first coupled
schema-change-plus-backfill lands.
