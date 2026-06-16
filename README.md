# forge-db

The Forge **database project**: a version-controlled tree of desired-state SQL scripts (one file per
object, dacpac-style) plus a C# deploy harness that reconciles any live Postgres database to that
desired state — Postgres' answer to a SQL Server `.dacpac` + `sqlpackage`.

The harness orchestrates [stripe/pg-schema-diff](https://github.com/stripe/pg-schema-diff)
(MIT-licensed, no account/registration) for the actual diff/apply; it does **not** hand-roll schema
diffing. EF Core in `forge-api` stops generating migrations and becomes a lean query-mapping layer
kept in sync via a CI drift-check.

> **Engine note:** we initially built over [Atlas](https://atlasgo.io/), but its free tier gates
> `CREATE EXTENSION/FUNCTION/TRIGGER` behind `atlas login` ("available to logged-in users only") —
> a non-starter for an open-source self-host stack. pg-schema-diff has no such gate and handles our
> `vector` columns, identity columns, functions, and triggers natively. The swap is contained to one
> file (`PgSchemaDiffRunner`).

> **Status: BUILT (Phase 2 scaffold).** The prerequisite EF migration squash is **merged**
> (forge-api `a8260a75`, PR #18) and **deployed** — the boot reconciler collapsed the live
> `__EFMigrationsHistory` to the baseline with data intact. From that proven baseline's canonical
> `pg_dump`, this repo now contains:
> - the desired-state **`schema/` tree** — 293 tables, 865 indexes, 2 functions, 2 triggers, 1
>   extension, one object per file;
> - the **`Forge.Db` harness** (`baseline` / `assemble` / `plan` / `verify` / `apply`) that drives
>   pg-schema-diff;
> - a **green round-trip**: `verify` shows zero diff against the baseline (pg-schema-diff covers
>   tables/indexes/constraints **and** functions/triggers/extensions), backed by an explicit
>   `pg_extension` / `pg_proc` / `pg_trigger` check as belt-and-suspenders — a trigger-drop test
>   trips **both** layers (§9 #1).
>
> Not yet done (separate efforts): the forge-api §5 lean-EF refactor + the one-directional
> drift-check CI, and the owner-gated **no-op `apply` handoff** to the live install (deploy hold
> stands). See [docs/DESIGN.md](docs/DESIGN.md): decision table (§7) and squash lessons (§9 — the
> ledger triggers the squash silently dropped, caught only by tests).
