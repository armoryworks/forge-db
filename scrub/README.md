# scrub/ — clean-rebuild garbage rules (INPUT)

Version-controlled cleanup SQL for the **clean-rebuild workflow** (docs/DESIGN.md §6.2): dump a
live system (`forge-db dump`), provision a fresh one (`forge-db apply` on an empty DB), load the
dump back (`forge-db import`) — and these scripts are where "garbage" is *defined*, so every
rebuild applies the same cleanup instead of someone's ad-hoc DELETEs.

Run by `import` **after** the data load and sequence fixup, **before** FK validation. Typical
residents: purge soft-deleted rows, expired tokens/sessions, orphaned attachments, dead-feature
leftovers that `--exclude` can't express because they share a table with live rows.

## Authoring convention

Same shape as `data/` and `seed/` — one concern per file, zero-padded numeric prefix
(`0010-purge-soft-deleted.sql`), lexicographic order — with **one deliberate difference**:

- **NOT applied-once.** There is no ledger entry for scrub scripts; **every import runs the full
  set**. Author them so a second run is a no-op — plain `DELETE`/`UPDATE` with `WHERE` guards is
  naturally idempotent.
- Each script runs in its own transaction; a failure rolls back that script and stops the import.
- Deleting a parent row? Delete (or re-point) its children **in the same script, children first** —
  the FK validation pass that follows will fail the import on any orphans you leave behind
  (that's the point).
- `import --skip-scrub` bypasses the whole directory for a faithful 1:1 restore.
