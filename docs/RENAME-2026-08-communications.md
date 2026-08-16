# The communications / attestations rename (2026-08) — no longer a manual step

`contact_interactions` → `communications`, `sales_order_acceptances` → `attestations`, plus
`user_id` → `handled_by_user_id` and `interaction_date` → `occurred_at`.

**Nothing to run by hand.** This is
[`premigrate/0010-rename-communications-attestations.sql`](../premigrate/0010-rename-communications-attestations.sql),
applied automatically as phase 0 of `forge-db apply` — before the schema reconcile, and before its
plan is even computed. See [DESIGN §6.3](DESIGN.md#63-pre-migrate-what-a-state-diff-cannot-express)
for why a rename needs its own phase, and [premigrate/README.md](../premigrate/README.md) for the
authoring contract.

This document previously carried the SQL as a runbook. It was the wrong shape: a rename that must
run correctly on *every* install, forever, cannot depend on an operator finding a markdown file.

## What to expect on a populated install

```bash
forge-db plan  --db "$DB"     # ⚠ warns that a pre-migrate script is pending
forge-db apply --db "$DB" --env dev --yes
```

`plan` is a pure read and does not run pre-migrate scripts, so on a not-yet-renamed target it prints
the plan that *would* run without them — including the `DROP TABLE … DELETES_DATA` this script
exists to prevent. It warns loudly above the plan when that is the case. **Do not reach for
`--allow-destructive` on the strength of that plan.** Run `apply`; the rename lands first and the
reconcile that follows is additive.

Verify afterwards:

```sql
SELECT count(*) FROM communications;   -- matches the pre-rename contact_interactions count
SELECT count(*) FROM attestations;     -- matches the pre-rename sales_order_acceptances count
```

Indexes, primary keys and FK constraints follow `RENAME TO` automatically but keep their old names,
so pg-schema-diff plans to drop and recreate them under the new ones. That is an index rebuild, not
data loss. On a large table, apply in a maintenance window.

## Caveat that outlives this rename: `dump` / `import` and renamed tables

`forge-db import` (DESIGN §6.2) matches dump files to target tables **by name**. A dump taken before
a rename lands in `MissingInTarget` on import and its rows are silently dropped. Rename the dump
files first, or take the dump after applying. This applies to any rename, not just this one — the
pre-migrate phase fixes the `apply` path, not the archive path.
