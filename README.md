# forge-db

The Forge **database project**: a version-controlled tree of desired-state SQL scripts (one file per
object, dacpac-style) plus a C# harness that reconciles any live Postgres database to that desired
state — Postgres' answer to a SQL Server `.dacpac` + `sqlpackage`.

The harness orchestrates [stripe/pg-schema-diff](https://github.com/stripe/pg-schema-diff)
(MIT-licensed, no account/registration) for the actual diff/apply; it does **not** hand-roll schema
diffing.

**This repo owns the Forge schema.** `forge-api` has no EF Core migrations — they were retired on
2026-06-17. EF Core there is a lean query-mapping layer, and the schema it maps onto is the
assembled output of this repo's `schema/` tree, embedded in the API as
`forge.data/Schema/forge-schema.sql`. The API's `SchemaBootstrapper` applies that file only to a
**fresh** database and is a no-op on an existing one; a populated install is brought forward by the
`forge-db` reconcile step in the `forge-deploy` upgrade sequence.

> **Engine note:** we initially built over [Atlas](https://atlasgo.io/), but its free tier gates
> `CREATE EXTENSION/FUNCTION/TRIGGER` behind `atlas login` ("available to logged-in users only") —
> a non-starter for an open-source self-host stack. pg-schema-diff has no such gate and handles our
> `vector` columns, identity columns, functions, and triggers natively. The swap is contained to one
> file (`PgSchemaDiffRunner`).

## What's in here

| Path | Role |
|------|------|
| `schema/` | **The desired state.** One object per file: `tables/`, `indexes/`, `functions/`, `triggers/`, `extensions/`, `views/`. This is the source of truth. |
| `premigrate/` | Applied-once scripts that run **before** the reconcile — the escape hatch for changes a state diff cannot express (renames, which would otherwise plan as DROP + CREATE). See [DESIGN.md §6.3](docs/DESIGN.md). |
| `data/` | Ordered, explicitly-idempotent backfills coupled to a schema change (add nullable column → backfill → enforce `NOT NULL`). pg-schema-diff emits DDL, never data. [§6.1](docs/DESIGN.md) |
| `seed/` | Schema-adjacent reference rows the application assumes exist (priorities, UoM, carriers, currencies, the chart-of-accounts reference, …). |
| `scrub/` | Cleanup rules for the clean-rebuild workflow — where "garbage" is defined once instead of as someone's ad-hoc `DELETE`. [§6.2](docs/DESIGN.md) |
| `history/` | Apply **receipts**: the plan SQL captured before each apply. Output only — never edited, never replayed. [§4.1](docs/DESIGN.md) |
| `src/Forge.Db` | The harness CLI. `tests/Forge.Db.Tests` covers it (unit + Postgres-backed). |
| `tools/apply-schema.sh` | Turn-key plan → apply → verify against the local stack. |

Full rationale, decision table, and the squash post-mortem: [docs/DESIGN.md](docs/DESIGN.md).

## Where this runs

- **Deploy.** The `Dockerfile` bakes the harness, the `schema/` tree, and the pg-schema-diff binary
  into a single image published to `ghcr.io/armoryworks/forge-db` (multi-arch, amd64 + arm64).
  `forge-deploy` runs it as a one-shot pre-update step — backup → schema reconcile → app swap →
  health gate — with `SCHEMA_IMAGE_TAG` pinned in lockstep with the release. Destructive plans halt
  and are enumerated for approval rather than applied.
- **CI.** `forge-api`'s `schema-drift-check` workflow re-assembles this repo's `schema/` tree and
  fails if the embedded `forge.data/Schema/forge-schema.sql` has drifted from it. Regenerate that
  file with `forge-db assemble` whenever the schema changes.

**A self-hoster who only runs the Forge stack installs none of the tooling below** — the reconcile
arrives as a container image. The prerequisites are for working on the schema or running the harness
by hand.

## Developing forge-db

Prerequisites:

- **.NET 10 SDK.**
- **[stripe/pg-schema-diff](https://github.com/stripe/pg-schema-diff)** — the diff engine (MIT, no
  account). It publishes **no prebuilt binaries**, so install via either:
  - `go install github.com/stripe/pg-schema-diff/cmd/pg-schema-diff@v1.0.5` (the pinned version), or
  - `brew install pg-schema-diff`.

  Make sure it's on `PATH` (e.g. `$(go env GOPATH)/bin`) or set `PG_SCHEMA_DIFF_BIN`.
- A **pgvector** Postgres (the schema declares the `vector` type, and pg-schema-diff provisions its
  own temp DB on the target server — the connecting user needs `CREATEDB`). The project targets
  `pgvector/pgvector:pg18`.

Common commands:

```bash
# inspect the assembled desired-state SQL (also how forge-api's embedded copy is regenerated)
dotnet run --project src/Forge.Db -- assemble --out /tmp/desired.sql

# show what would change to reconcile a DB to schema/ (no mutation)
dotnet run --project src/Forge.Db -- plan   --db "postgres://user:pw@host:5432/db?sslmode=disable"

# assert a DB matches schema/ — exit non-zero on drift. Runs the pg-schema-diff plan PLUS an
# explicit pg_extension / pg_proc / pg_trigger check, so a dropped trigger trips both layers.
dotnet run --project src/Forge.Db -- verify --db "postgres://user:pw@host:5432/db?sslmode=disable"

# reconcile behind the safety gates: pending premigrate/ scripts first, then the schema plan
# (captured to history/ before it runs), then pending data/ + seed/ scripts — each applied once
dotnet run --project src/Forge.Db -- apply  --db "postgres://…/db" --env dev

# clean rebuild (docs/DESIGN.md §6.2): dump data out, provision fresh, import back minus garbage
dotnet run --project src/Forge.Db -- dump   --db "postgres://…/old"   --out ./dump
dotnet run --project src/Forge.Db -- import --db "postgres://…/fresh" --from ./dump --exclude 'audit_*,*_log'

# (one-time) re-seed schema/ from a canonical pg_dump --schema-only
dotnet run --project src/Forge.Db -- baseline --dump baseline.schema.sql

dotnet run --project src/Forge.Db -- version   # harness + engine versions
dotnet test tests/Forge.Db.Tests
```

### Turn-key apply (`tools/apply-schema.sh`)

For the common "reconcile the running stack's DB to `schema/`" case there is a wrapper that resolves
the DB URL from the forge-deploy compose conventions (`../forge-deploy/.env`, falling back to
`postgres/postgres@localhost:5432/forge`), checks the diff engine is installed, always shows the
plan first, applies through the harness's `DeployGates` (dev targets auto-confirm; non-dev requires
`--yes --backup-taken`, destructive plans always require `--allow-destructive`), and finishes with a
`verify` round-trip:

```bash
tools/apply-schema.sh                 # plan → apply → verify against the local stack
tools/apply-schema.sh --plan-only     # look, don't touch
tools/apply-schema.sh --env prod --yes --backup-taken   # gated non-dev apply
```

## Deploying Forge

This repository is a component of the **[Forge](https://github.com/armoryworks/forge)** platform. To
deploy or update the full Forge application, use the
**[`@armoryworks/forge-deploy`](https://github.com/armoryworks/forge-deploy)** installer — a thin
bootstrapper that fetches the current deploy tree from GitHub and hands off to setup:

```bash
sudo mkdir -p /opt/forge-deploy && sudo chown "$USER:$(id -gn)" /opt/forge-deploy

npx @armoryworks/forge-deploy /opt/forge-deploy    # first install (pulls images from GHCR)
npx @armoryworks/forge-deploy upgrade              # refresh the tree + run the gated upgrade
```

Re-running preserves your `.env`, compose overrides, and data volumes. See the
**[forge-deploy README](https://github.com/armoryworks/forge-deploy#readme)** for full deploy,
topology, and troubleshooting docs.

## License

Apache License 2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).
