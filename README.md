# forge-db

The Forge **database project**: a version-controlled tree of desired-state SQL scripts (one file per
object, dacpac-style) plus a C# deploy harness that reconciles any live Postgres database to that
desired state — Postgres' answer to a SQL Server `.dacpac` + `sqlpackage`.

The harness orchestrates [Atlas](https://atlasgo.io/) (declarative Postgres schema engine) for the
actual diff/apply; it does **not** hand-roll schema diffing. EF Core in `forge-api` stops generating
migrations and becomes a lean query-mapping layer kept in sync via a CI drift-check.

> **Status: design agreed; prerequisite executed, forge-db itself not built yet.** All key decisions
> are settled (§7). The prerequisite EF migration squash — whose `InitialBaseline` seeds this repo's
> `schema/` tree — is done and verified (forge-api PR #18, awaiting merge). See
> [docs/DESIGN.md](docs/DESIGN.md): full design + decision table (§7), and **§9 — lessons from the
> squash that forge-db must honor** (notably: Atlas's diff does not cover triggers/functions, so the
> drift-check must compare them explicitly).
