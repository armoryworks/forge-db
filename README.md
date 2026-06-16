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

## Developing forge-db

**This tooling is only needed to work on the schema or run the harness — not to run the Forge app.**
The app gets its schema from forge-api today; forge-db is a dev / CI / deploy-time tool, so a
self-hoster who only runs the stack never installs any of this.

Prerequisites:
- **.NET 10 SDK** + the EF tool: `dotnet tool install --global dotnet-ef`.
- **[stripe/pg-schema-diff](https://github.com/stripe/pg-schema-diff)** — the diff engine (MIT, no
  account). It ships **no prebuilt binaries**, so install via either:
  - `go install github.com/stripe/pg-schema-diff/cmd/pg-schema-diff@v1.0.5` (pinned), or
  - `brew install pg-schema-diff`.

  Make sure it's on `PATH` (e.g. `$(go env GOPATH)/bin`) or set `PG_SCHEMA_DIFF_BIN`.
- A **pgvector** Postgres (the schema declares the `vector` type, and pg-schema-diff provisions its
  own temp DB on the target server — the connecting user needs `CREATEDB`). The project targets
  `pgvector/pgvector:pg17`.

Common commands:

```bash
# (one-time) seed schema/ from a canonical pg_dump --schema-only of the squashed baseline
dotnet run --project src/Forge.Db -- baseline --dump baseline.schema.sql

# inspect the assembled desired-state SQL
dotnet run --project src/Forge.Db -- assemble --out /tmp/desired.sql

# show what would change to reconcile a DB to schema/ (no mutation)
dotnet run --project src/Forge.Db -- plan   --db "postgres://user:pw@host:5432/db?sslmode=disable"

# assert a DB matches schema/ — exit non-zero on drift (this is what CI runs)
dotnet run --project src/Forge.Db -- verify --db "postgres://user:pw@host:5432/db?sslmode=disable"

dotnet test tests/Forge.Db.Tests
```

CI runs that same `verify` against the EF model via forge-api's `schema-drift-check` workflow.

