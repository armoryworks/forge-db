# forge-db

The Forge **database project**: a version-controlled tree of desired-state SQL scripts (one file per
object, dacpac-style) plus a C# deploy harness that reconciles any live Postgres database to that
desired state — Postgres' answer to a SQL Server `.dacpac` + `sqlpackage`.

The harness orchestrates [Atlas](https://atlasgo.io/) (declarative Postgres schema engine) for the
actual diff/apply; it does **not** hand-roll schema diffing. EF Core in `forge-api` stops generating
migrations and becomes a lean query-mapping layer kept in sync via a CI drift-check.

> **Status: BUILT (Phase 2 scaffold).** The prerequisite EF migration squash is **merged**
> (forge-api `a8260a75`, PR #18) and **deployed** — the boot reconciler collapsed the live
> `__EFMigrationsHistory` to the baseline with data intact. From that proven baseline's canonical
> `pg_dump`, this repo now contains:
> - the desired-state **`schema/` tree** — 293 tables, 865 indexes, 2 functions, 2 triggers, 1
>   extension, one object per file;
> - the **`Forge.Db` harness** (`baseline` / `plan` / `verify` / `apply`) that orchestrates Atlas;
> - a **green round-trip**: `verify` shows zero diff against the baseline, and the explicit
>   `pg_proc` / `pg_trigger` / `pg_extension` check catches what Atlas can't — proven by a
>   trigger-drop test where Atlas reports "synced" while `verify` correctly fails (§9 #1).
>
> Not yet done (separate efforts): the forge-api §5 lean-EF refactor + the one-directional
> drift-check CI, and the owner-gated **no-op `apply` handoff** to the live install (deploy hold
> stands). See [docs/DESIGN.md](docs/DESIGN.md): decision table (§7) and squash lessons (§9 —
> notably that Atlas covers neither triggers/functions nor, on the free tier, extensions).
