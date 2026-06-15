# forge-db — Design

> **Status: DESIGN AGREED — not built yet.** `forge-db` is an empty repo
> (`armoryworks/forge-db`). The §7 decisions are settled; this doc defines what forge-db becomes and
> how it relates to the EF migration squash in
> [`forge-api/docs/db/MIGRATION_SQUASH_PLAN.md`](../../forge-api/docs/db/MIGRATION_SQUASH_PLAN.md).
> Cross-refs: [[schema-migration-direction]].

---

## 1. What forge-db is (and the dacpac analogy)

forge-db is a **database project**: a hand-organized, version-controlled tree of SQL scripts that
declares the **desired state** of the Postgres schema — one file per object (table, view, function,
index, extension) — plus a **C# deploy harness** that brings any live database up to that desired
state.

It exists because **Postgres has no dacpac.** In the SQL Server world, a `.sqlproj` compiles to a
`.dacpac` and `sqlpackage` diffs that desired-state package against a live DB and generates the
exact `ALTER`/`CREATE`/`DROP` set to reconcile it — declarative, idempotent, no hand-written
migration chain. forge-db reproduces that *model* for Postgres:

| dacpac / SQL Server | forge-db / Postgres |
|---|---|
| `.sqlproj` (per-object SQL scripts = desired state) | the `schema/` script tree (§3) |
| `.dacpac` (compiled desired state) | the assembled desired-state SQL handed to the diff engine |
| `sqlpackage /Action:Publish` (diff + apply) | the C# harness orchestrating Atlas (§4) |
| `__SchemaVersion` bookkeeping | Postgres' own catalog + Atlas' applied-state tracking |

**Key decision (settled):** the harness does **not** hand-roll the schema-diff logic. Computing a
correct `ALTER` set from "desired vs live" for Postgres is exactly what **Atlas** (declarative,
first-class Postgres) already does — `migra` is the fallback engine. The C# is **coordination /
scaffolding**: it owns *when*, *against what*, *in what order*, with *what safety gates* — Atlas
owns the diff correctness. This is the cheap, low-risk half of the dacpac model; reimplementing the
diff engine is the expensive, correctness-critical half we deliberately skip.

---

## 2. Relationship to the EF squash (sequencing)

forge-db does **not** replace the squash — it consumes its output. Per the squash plan, Phase 1
(collapse the 132 EF migrations / ~268 files / 121 MB into one `InitialBaseline`) is the
prerequisite **regardless** of declarative direction, because the baseline is what produces clean,
canonical SQL.

```
Phase 1 (forge-api)            Phase 2 (forge-db) — this doc
─────────────────────         ──────────────────────────────
squash 132 → InitialBaseline   seed schema/ tree from baseline pg_dump
  │  prove schema-equivalence    │  stand up C# harness over Atlas
  │  (§3.3 of squash plan)       │  CI drift-check: EF model ⟷ forge-db schema
  ▼                              ▼
pg_dump --schema-only  ───────▶  schema/ desired-state scripts
(the canonical seed)            EF stops generating migrations
```

The squash's `pg_dump --schema-only --no-owner --no-privileges` of the proven baseline is the
**one-time seed** for the `schema/` tree. After that, forge-db owns schema; EF never generates
another migration.

---

## 3. Repository layout

```
forge-db/
├── schema/                     # desired state (INPUT) — one object per file, dacpac-style
│   ├── extensions/             #   CREATE EXTENSION (vector, etc.)
│   ├── tables/                 #   one CREATE TABLE per file, incl. its PK/FK/constraints
│   ├── indexes/                #   non-trivial / filtered / composite indexes
│   ├── views/
│   └── functions/
│   #  NOTE: no enums/ dir — enums stay int + reference_data (§ decision 4)
├── data/                       # ordered, explicitly-idempotent backfill scripts (INPUT) — see §6.1
├── seed/                       # reference/lookup data that is schema-adjacent (reference_data groups)
├── history/                    # captured apply plans (OUTPUT, audit-only, non-replayable) — see §4.1
├── src/Forge.Db/               # C# deploy harness (the "sqlpackage" role)
│   ├── Program.cs              #   CLI entrypoint: plan | apply | verify | baseline
│   └── ...
├── atlas.hcl                   # Atlas project: dev-DB URL, schema src = ./schema
├── tests/                      # harness tests + golden schema-equivalence fixtures
└── docs/DESIGN.md              # this file
```

**INPUT vs OUTPUT is load-bearing.** `schema/` and `data/` are *inputs* — the source of truth you
edit. `history/` is an *output* — receipts of what was applied, never edited, never replayed (§4.1).
Conflating the two is the failure mode this layout exists to prevent.

**Authoring convention:** each file is the **final desired definition** of exactly one object — not
a migration step. You edit `tables/part.sql` to add a column; you never write an `ALTER`. The diff
engine derives the `ALTER`. This is the single most important discipline: the tree is *state*, not
*history*.

**Naming/constraint discipline (carried from the squash plan §4):** explicit, snake_case
constraint and index names on every object. The accounting `acct_*` configs already do this; it's
what lets the baseline export to clean SQL and lets Atlas produce stable, readable diffs.

---

## 4. The C# harness ↔ Atlas boundary

The harness is a thin .NET CLI (`Forge.Db`) that orchestrates Atlas and enforces Forge-specific
safety. Verbs:

| Verb | What the harness does | What Atlas does |
|---|---|---|
| `plan` | resolve target DB, render the SQL Atlas *would* run for review (no mutation) | `atlas schema apply --dry-run` (desired `schema/` vs live) |
| `apply` | gate (env, backup taken, not-live-without-confirm, destructive policy), **capture the plan SQL to `history/` (§4.1)**, run any due `data/` backfills, apply, log `[DB-LIFECYCLE]`-style | `atlas schema apply` |
| `verify` | assert live DB == desired state, exit non-zero on drift (for CI) | `atlas schema diff` → expect empty |
| `baseline` | (one-time) ingest the squash `pg_dump` into `schema/` | — |

**Why C# and not just the Atlas CLI:** the coordination Forge needs lives above the diff —
environment guardrails (never auto-apply against the Armory Plastics live DB; require an explicit
flag + a fresh backup), the destructive-change policy (block column/table drops unless
`--allow-destructive` is passed for that run, mirroring dacpac's `BlockOnPossibleDataLoss`),
capturing the audit receipt, sequencing data backfills around the schema apply, structured logging
consistent with the existing boot logs, and exit-code contracts for CI. Language is C# to match the
rest of the stack and reuse config/secrets plumbing; nothing here is C#-specific if a better fit
emerges.

**Idempotency is free for DDL — do not conflate it with the archive.** `atlas schema apply` is
inherently idempotent: a second run computes an empty diff and is a no-op. The harness does **not**
hand-generate SQL to *achieve* idempotency; declarative apply already provides it. The only place
idempotency must be hand-engineered is data backfills (§6.1), which Atlas does not generate.

### 4.1 The audit archive (`history/`)

We deploy declarative-pure (no versioned migration files as the apply mechanism — §decision 2), but
we still want a human-readable record of what each deploy changed. So on every `apply` the harness
**captures Atlas's own plan SQL** (`schema apply --dry-run` output) and writes it to
`history/<timestamp>-<env>.sql` *before* applying.

- **Atlas generates the SQL; the harness only captures it.** C# never computes a diff or synthesizes
  DDL — that would rebuild the one thing we chose not to build.
- **Audit-only, non-replayable.** These files are *receipts, not recipes.* The `schema/` tree + Atlas
  remain the only things that determine state. Replaying a `history/` file is never a supported
  operation; the harness will not read from `history/`. (Optionally also write a row to a
  `schema_change_log` table for in-DB audit — same audit-only rule.)
- This recovers the one thing declarative-pure gives up (a per-change reviewable artifact) without
  making that artifact a second source of truth.

---

## 5. EF Core's new role (lean mapping)

Once forge-db owns schema, EF's model is **no longer schema-defining — only query-mapping.** This is
why the forge-api CLAUDE.md rules flipped to *prefer attributes, avoid `OnModelCreating`*.

**What EF keeps** (the minimum to query correctly):
- **Attributes on entities** for table/column/key/FK/length/precision mapping (`[Table]`,
  `[Column]`, `[MaxLength]`, `[Precision]`, `[ForeignKey]`).
- **snake_case** via `EFCore.NamingConventions` (`.UseSnakeCaseNamingConvention()` at the
  options-builder level in `Program.cs`) — avoids hand-annotating `[Column("snake")]` on thousands
  of properties.
- **`SaveChanges` interceptors** — `SetTimestamps` / `NormalizeDateTimes` are runtime behavior,
  untouched by all of this.
- **The soft-delete global query filter** — the one irreducible `OnModelCreating` concern. EF Core
  has **no attribute equivalent** for `HasQueryFilter`, so this stays as model config
  ([`AppDbContext.cs:559-571`](../../forge-api/forge.data/Context/AppDbContext.cs#L559)).

**What EF sheds** (forge-db owns it now, so it simply *leaves* EF rather than converting to
attributes): index definitions, FK constraint *names*, check constraints, filtered/partial indexes,
the `vector` extension declaration. A large share of the 278 `IEntityTypeConfiguration` files is
schema description that deletes outright.

**Drift control — the contract that keeps the two in sync (one-directional):** forge-db is
authoritative, so the check asserts *EF conforms to forge-db* — if they differ, **EF is wrong**, not
forge-db. Mechanism (§decision 5): **forge-api CI checks out forge-db at a pinned ref**, builds a
scratch DB from `schema/`, builds another from the EF model, runs `Forge.Db verify`, and fails the
build on any diff. No submodule, no publish pipeline — the check lives where the EF model lives. This
is the answer to the squash plan's "EF still needs the C# model" thorny question — option (a): EF
keeps the mapping, forge-db owns schema, CI enforces conformance. We are **not** scaffolding the
model from the DB (option b).

---

## 6. Where deploy runs — and the live-data problem

**Today:** the `forge-api` container runs `MigrateAsync()` on boot ([`Program.cs` ~1209-1365](../../forge-api/forge.api/Program.cs#L1209)),
with a self-healing verifier ([`MigrationSchemaVerifier.cs`](../../forge-api/forge.data/Migrations/MigrationSchemaVerifier.cs))
for missing-history cases. Deploy is docker-compose (`forge-deploy`); a failed migration makes the
container unhealthy and rollback restores the prior image.

**Target (§decision 1): deploy-time apply, read-only boot.** `Forge.Db apply` runs as an explicit
step in `forge-deploy` (or CI) *before* the new API image goes live. The API container's boot
becomes **read-only**: it runs `Forge.Db verify` and **refuses to start on drift**, but never
mutates schema — `MigrateAsync()` is removed from the boot path. This matches the dacpac mental
model (publish is a deploy action, not an app side-effect), removes schema mutation from the hot
path, and makes every schema change a deliberate, observable step with its own backup gate. (The
rejected alternative was boot-time apply, which keeps schema mutation coupled to container start.)

**The Armory Plastics live-data reconciliation does not disappear.** Their DB already
holds real data and the 132 historical migration IDs. The cutover sequence is:
1. Land Phase 1 squash + its boot reconciliation (squash plan §3.1) — this is what makes their
   `__EFMigrationsHistory` sane *before* forge-db is in the picture.
2. Stand up forge-db `schema/` from the proven baseline; `verify` against an Armory Plastics
   **clone** (never the live DB) shows **zero diff** — proving the desired state already matches
   what they're running. The first forge-db deploy against them is therefore a **no-op apply**,
   which is the safe way to take ownership.
3. Only *after* that no-op handoff does forge-db become the mutation path for them.

### 6.1 Data backfills (the real idempotency gap)

Atlas generates **DDL**, never **data**. The classic coupled change — add a `NOT NULL` column →
backfill existing rows → enforce the constraint — cannot be expressed as pure desired state. So
forge-db needs a `data/` area for hand-written backfills, run in coordination with the schema apply:

- **Ordered + applied-once + tracked.** Each script runs once; the harness records applied scripts
  (a `data_migration_log` table) so re-deploys skip them. This is the *one* place forge-db is
  change-based rather than declarative — and it's deliberate, scoped to data.
- **Explicitly idempotent anyway.** Scripts are written to be safe if re-run (guard with
  `WHERE`/`NOT EXISTS`), belt-and-suspenders on top of the applied-once log.
- **Sequencing with DDL.** Where a backfill must interleave (add nullable column → backfill → set
  `NOT NULL`), the harness applies it as: schema apply (nullable col) → due `data/` scripts →
  schema apply (constraint). Splitting such a change across two desired-state steps + a backfill is
  a documented authoring pattern, not an Atlas feature.

This is distinct from the `history/` audit archive (§4.1): `data/` is an *input* you author;
`history/` is an *output* you never touch.

---

## 7. Decisions (settled)

| # | Decision | Choice |
|---|---|---|
| 1 | **Deploy location** | Deploy-time `apply` in forge-deploy/CI; **boot is read-only** (`verify`, refuse-on-drift). `MigrateAsync()` removed from boot. (§6) |
| 2 | **Atlas model** | **Declarative-pure** (`atlas schema apply`). No versioned migration files as the apply mechanism; audit handled by capturing the plan to `history/` (§4.1). |
| 3 | **Destructive-change policy** | **Block by default**, override per-run with `--allow-destructive` (dacpac `BlockOnPossibleDataLoss` parity). (§4) |
| 4 | **Enum strategy** | **Keep `int` + `reference_data`.** No native PG enums — they fight declarative apply (`ALTER TYPE ADD VALUE`) and app-layer enforcement already exists. No `schema/enums/` dir. |
| 5 | **Repo coupling / drift-check** | **One-directional**: forge-api CI checks out forge-db at a pinned ref and `verify`s the EF model conforms to `schema/`. EF wrong on mismatch. No submodule, no artifact pipeline. (§5) |

Two consequences worth restating: forge-db is **change-based in exactly one place** — `data/`
backfills (§6.1) — and declarative everywhere else; and the `history/` archive is **audit-only and
never replayed** (§4.1).

---

## 8. What stays true regardless

- **Still gated.** This is schema-tooling only; it does not enable any dark capability and does not,
  by itself, touch the Armory Plastics live DB. Deployment remains held by the owner gate.
- **Squash is the prerequisite.** No forge-db work ships before Phase 1 lands and its
  schema-equivalence proof is green.
- **Change-based tools remain rejected.** Sqitch/Flyway/Liquibase are not the target; forge-db is
  declarative/state-based by design.
