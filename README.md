# forge-db

The Forge **database project**: a version-controlled tree of desired-state SQL scripts (one file per
object, dacpac-style) plus a C# deploy harness that reconciles any live Postgres database to that
desired state — Postgres' answer to a SQL Server `.dacpac` + `sqlpackage`.

The harness orchestrates [Atlas](https://atlasgo.io/) (declarative Postgres schema engine) for the
actual diff/apply; it does **not** hand-roll schema diffing. EF Core in `forge-api` stops generating
migrations and becomes a lean query-mapping layer kept in sync via a CI drift-check.

> **Status: design agreed — nothing built yet.** All key decisions are settled. See
> [docs/DESIGN.md](docs/DESIGN.md) for the full design, sequencing against the EF migration squash,
> and the settled decision table (§7).
