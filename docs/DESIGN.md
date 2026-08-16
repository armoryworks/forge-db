# forge-db — Design

> **Status: DESIGN AGREED · Phase 2 scaffold BUILT.** The §7 decisions are settled, and the
> `schema/` tree + `Forge.Db` harness now exist with a green `verify` round-trip (see README). This
> doc defines what forge-db is and how it relates to the EF migration squash in
> [`forge-api/docs/db/MIGRATION_SQUASH_PLAN.md`](../../forge-api/docs/db/MIGRATION_SQUASH_PLAN.md)
> (merged forge-api `a8260a75`, deployed). Cross-refs: [[schema-migration-direction]].

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
| `sqlpackage /Action:Publish` (diff + apply) | the C# harness orchestrating pg-schema-diff (§4) |
| `__SchemaVersion` bookkeeping | Postgres' own catalog (pg-schema-diff is stateless — it diffs desired-vs-live each run, no applied-state tracking) |

**Key decision (settled):** the harness does **not** hand-roll the schema-diff logic. Computing a
correct `ALTER` set from "desired vs live" for Postgres is exactly what **pg-schema-diff**
(stripe/pg-schema-diff, MIT, no registration) already does. The C# is **coordination /
scaffolding**: it owns *when*, *against what*, *in what order*, with *what safety gates* —
pg-schema-diff owns the diff correctness. This is the cheap, low-risk half of the dacpac model;
reimplementing the diff engine is the expensive, correctness-critical half we deliberately skip.

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
  │  prove schema-equivalence    │  stand up C# harness over pg-schema-diff
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
│   ├── functions/              #   plpgsql functions (e.g. ledger immutability — see §9)
│   └── triggers/               #   triggers (raw SQL; first-class here, unlike EF — see §9)
│   #  NOTE: no enums/ dir — enums stay int + reference_data (§ decision 4)
├── premigrate/                 # applied-once scripts run BEFORE the reconcile (INPUT) — see §6.3
│   #  the escape hatch for what a state diff cannot express: renames
├── data/                       # ordered, explicitly-idempotent backfill scripts (INPUT) — see §6.1
├── seed/                       # reference/lookup data that is schema-adjacent (reference_data groups)
├── history/                    # captured apply plans (OUTPUT, audit-only, non-replayable) — see §4.1
├── src/Forge.Db/               # C# deploy harness (the "sqlpackage" role)
│   ├── Program.cs              #   CLI entrypoint: plan | apply | verify | baseline
│   └── ...
│   #  NOTE: no project config file — the harness drives pg-schema-diff purely via CLI flags
├── tests/                      # harness tests + golden schema-equivalence fixtures
└── docs/DESIGN.md              # this file
```

**INPUT vs OUTPUT is load-bearing.** `schema/`, `premigrate/` and `data/` are *inputs* — the source of truth you
edit. `history/` is an *output* — receipts of what was applied, never edited, never replayed (§4.1).
Conflating the two is the failure mode this layout exists to prevent.

**Authoring convention:** each file is the **final desired definition** of exactly one object — not
a migration step. You edit `tables/part.sql` to add a column; you never write an `ALTER`. The diff
engine derives the `ALTER`. This is the single most important discipline: the tree is *state*, not
*history*.

**Naming/constraint discipline (carried from the squash plan §4):** explicit, snake_case
constraint and index names on every object. The accounting `acct_*` configs already do this; it's
what lets the baseline export to clean SQL and lets pg-schema-diff produce stable, readable diffs.

---

## 4. The C# harness ↔ pg-schema-diff boundary

The harness is a thin .NET CLI (`Forge.Db`) that orchestrates pg-schema-diff and enforces
Forge-specific safety. Verbs:

| Verb | What the harness does | What pg-schema-diff does |
|---|---|---|
| `plan` | resolve target DB, render the migration SQL pg-schema-diff *would* run for review (no mutation); an empty plan means in-sync | `pg-schema-diff plan --from-dsn <db> --to-dir <dir>` (desired `schema/` vs live) |
| `apply` | gate (env, backup taken, not-live-without-confirm, destructive policy), **capture the plan SQL to `history/` (§4.1)**, run any due `data/` backfills, apply, log `[DB-LIFECYCLE]`-style | `pg-schema-diff apply --from-dsn <db> --to-dir <dir> --skip-confirm-prompt --allow-hazards <LIST>` |
| `verify` | assert live DB == desired state, exit non-zero on drift (for CI) — **also compares triggers/functions explicitly as belt-and-suspenders (§9)** | `pg-schema-diff plan` → expect empty |
| `baseline` | (one-time) ingest the squash `pg_dump` into `schema/` | — |

> ✅ **The engine now covers triggers/functions/extensions — but we keep an explicit check anyway
> (learned from the squash — §9).** pg-schema-diff **does** diff functions and triggers and runs
> `CREATE EXTENSION` natively, so the engine itself now covers the objects that Atlas's free tier
> couldn't (a strict improvement over Atlas-free, which gated extensions/functions/triggers behind
> registration). It also natively handles our pgvector `vector(384)` columns and identity columns.
> **Even so, forge-db keeps the explicit `pg_extension` / `pg_proc` / `pg_trigger` comparison
> (`SchemaObjectVerifier`) as belt-and-suspenders**, justified by (a) the squash lesson that a silent
> diff gap is catastrophic for ledger immutability — a single missed trigger broke append-only — and
> (b) pg-schema-diff's own documented caveat that non-SQL function dependencies are *untrackable*
> (it flags `HAS_UNTRACKABLE_DEPENDENCIES`). So `verify`/`plan` rely on pg-schema-diff for the diff
> **and** explicitly compare `pg_extension`, `pg_proc` (functions), and `pg_trigger` (triggers). This
> is enforced by a regression: drop a ledger trigger and the trigger-drop now trips **both** layers —
> pg-schema-diff plans the re-create *and* the explicit `SchemaObjectVerifier` check fails.
>
> **The EF-history table is kept out of the diff by injection, not exclusion.** Rather than an
> exclude selector, the harness injects the exact `__EFMigrationsHistory` DDL into the assembled
> desired state as a *keep-alive*, so pg-schema-diff sees it on both sides and never plans a `DROP`.
> (Atlas used a schema-qualified `--exclude`; that mechanism is gone.)

**Desired state is a directory of plain DDL.** pg-schema-diff takes a `--to-dir` of `.sql` files
applied in **statement order (no topological sort)**, so the harness's `DesiredStateAssembler` emits
a single ordered file: extensions → tables → FKs → indexes → functions → triggers → the injected
`__EFMigrationsHistory` keep-alive. pg-schema-diff also **auto-manages its own temporary database**
on the target server to compute the diff (the connecting user needs `CREATEDB`); there is **no
separate dev-URL to provision** — the harness needs no dev-DB bootstrap step.

**Why C# and not just the pg-schema-diff CLI:** the coordination Forge needs lives above the diff —
environment guardrails (never auto-apply against the Armory Plastics live DB; require an explicit
flag + a fresh backup), the destructive-change policy (pg-schema-diff flags *hazards* — e.g.
`DELETES_DATA`, `INDEX_BUILD`, `HAS_UNTRACKABLE_DEPENDENCIES` — and `apply` must `--allow-hazards`
them; the harness's `DeployGates` treat `DELETES_DATA` and `DROP` statements as destructive and
**block them unless `--allow-destructive` is passed** for that run, mirroring dacpac's
`BlockOnPossibleDataLoss`), capturing the audit receipt, sequencing data backfills around the schema
apply, structured logging consistent with the existing boot logs, and exit-code contracts for CI.
Language is C# to match the rest of the stack and reuse config/secrets plumbing; nothing here is
C#-specific if a better fit emerges.

**Idempotency is free for DDL — do not conflate it with the archive.** `pg-schema-diff apply` is
inherently idempotent: a second run computes an empty plan and is a no-op. The harness does **not**
hand-generate SQL to *achieve* idempotency; declarative apply already provides it. The only place
idempotency must be hand-engineered is data backfills (§6.1), which pg-schema-diff does not generate.

### 4.1 The audit archive (`history/`)

We deploy declarative-pure (no versioned migration files as the apply mechanism — §decision 2), but
we still want a human-readable record of what each deploy changed. So on every `apply` the harness
**captures pg-schema-diff's own `plan` output** and writes it to
`history/<timestamp>-<env>.sql` *before* applying.

- **pg-schema-diff generates the SQL; the harness only captures it.** C# never computes a diff or
  synthesizes DDL — that would rebuild the one thing we chose not to build.
- **Audit-only, non-replayable.** These files are *receipts, not recipes.* The `schema/` tree +
  pg-schema-diff remain the only things that determine state. Replaying a `history/` file is never a
  supported operation; the harness will not read from `history/`. (Optionally also write a row to a
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

pg-schema-diff generates **DDL**, never **data**. The classic coupled change — add a `NOT NULL`
column → backfill existing rows → enforce the constraint — cannot be expressed as pure desired
state. So forge-db needs a `data/` area for hand-written backfills, run in coordination with the
schema apply:

- **Ordered + applied-once + tracked.** Each script runs once; the harness records applied scripts
  (a `data_migration_log` table) so re-deploys skip them. This is the *one* place forge-db is
  change-based rather than declarative — and it's deliberate, scoped to data.
- **Explicitly idempotent anyway.** Scripts are written to be safe if re-run (guard with
  `WHERE`/`NOT EXISTS`), belt-and-suspenders on top of the applied-once log.
- **Sequencing with DDL.** Where a backfill must interleave (add nullable column → backfill → set
  `NOT NULL`), the harness applies it as: schema apply (nullable col) → due `data/` scripts →
  schema apply (constraint). Splitting such a change across two desired-state steps + a backfill is
  a documented authoring pattern, not a pg-schema-diff feature.

This is distinct from the `history/` audit archive (§4.1): `data/` is an *input* you author;
`history/` is an *output* you never touch.

> **Status: BUILT.** `DataSeedRunner` implements this for both `data/` and `seed/`: ordered by
> zero-padded filename prefix (`data/` before `seed/`), applied-once via a ledger, each script in its
> own transaction, wired into `apply` after the schema reconcile (and it still runs when the schema is
> already in sync, so new seed scripts land). The ledger is `forge_db.data_migration_log` — a
> harness-owned `forge_db` schema **excluded from the pg-schema-diff reconcile** (same mechanism as
> `hangfire`), so it is neither desired-state you edit nor a source of EF-drift false positives. On
> non-dev targets the data/seed phase inherits the schema gate (`--yes --backup-taken`). Authoring
> convention: [data/README.md](../data/README.md). The directories are still empty — the forge-api
> reference seeders have not been ported yet (that extraction is the next effort).

### 6.2 Data dump & clean-rebuild import

Sometimes the right fix for an aging install isn't another migration — it's a **clean rebuild**:
dump the data, provision a fresh database from `schema/`, and load the data back *minus the
garbage*. The harness makes that a first-class, repeatable workflow instead of a pile of ad-hoc
`pg_dump`/`psql` invocations:

```bash
forge-db dump   --db postgres://…/old --out ./dump                       # 1. data out (read-only)
createdb forge_clean && forge-db apply --db postgres://…/forge_clean     # 2. fresh desired-state DB
forge-db import --db postgres://…/forge_clean --from ./dump \
                --exclude 'audit_*,*_log'                                # 3. data back, minus garbage
```

- **`dump`** streams every application table as `COPY … TO STDOUT` **text** (one file per table +
  `manifest.json`: columns, row counts, checksums, and a fingerprint of the assembled desired
  state). Text over binary deliberately: stable across PG versions, diffable, and — because the
  manifest records the column list — import loads the **intersection** of dumped and current
  columns, tolerating modest schema evolution between dump and import (dropped columns fall away,
  new columns take their defaults). The `hangfire`/`forge_db` schemas and `__EFMigrationsHistory`
  are excluded, mirroring the reconcile exclusions — infrastructure re-creates itself.
- **`import`** is the garbage filter, applied at three layers, in order:
  1. **`--exclude` globs** — whole tables that don't come along (event logs, dead features);
  2. **`scrub/` scripts** — version-controlled cleanup SQL run after the load, *every* import (NOT
     applied-once — the one deliberate divergence from `data/`/`seed/`; see
     [scrub/README.md](../scrub/README.md)): soft-deleted rows, expired tokens, orphaned blobs;
  3. **FK validation** — the load runs with FK triggers suspended
     (`session_replication_role = replica`, hence the superuser requirement), so a final pass
     re-checks every FK and **fails the import (exit 4) on orphans** unless `--allow-fk-orphans`.
     Orphans are precisely the garbage this workflow exists to surface, not a nuisance to paper
     over.

  The load itself is one transaction (`TRUNCATE … RESTART IDENTITY CASCADE` over the selected
  tables, then per-table `COPY … FROM STDIN`), so a failure leaves the target as `apply` provisioned
  it. Afterwards: serial/identity sequences are bumped past `max(id)`, `ANALYZE` refreshes stats,
  and a JSON receipt lands in `history/` beside apply's plan captures (audit-only, never replayed).
  Import truncates, so non-dev targets sit behind the same `--yes --backup-taken` posture as a
  schema apply.

**In-app equivalent (forge-api/forge-ui).** The same workflow is exposed to admins at
**Admin → Database Transfer** (`/admin/database`, `Admin` role): Export downloads the dump as a
**zip of this exact directory layout**, and Import loads such a zip back with the same three
garbage layers — exclude globs, a soft-deleted purge (the app-level `deleted_at` notion of garbage,
standing in for `scrub/`), and the FK-orphan report. Archives are interchangeable in both
directions: unzip a UI export and `forge-db import --from` it, or zip a CLI dump and upload it.
The CLI remains the path for a *cross-database* rebuild (dump old → `apply` a fresh DB → import),
since the in-app import necessarily targets the install it runs in.

> **Status: BUILT.** `DataDumper`/`DataImporter` + the `dump`/`import` verbs, covered by unit tests
> (glob semantics, COPY-text row projection, manifest round-trip) and a DB round-trip integration
> test (dump → import with an exclusion + scrub → row counts, sequence continuation, orphan
> detection) that runs in CI against the release workflow's Postgres service. `scrub/` is empty
> until the first garbage rule is authored.

---

### 6.3 Pre-migrate: what a state diff cannot express

pg-schema-diff compares **states**. That is the whole point of the harness — and it is also the one
thing it cannot do: it has no way to know that two states are related by a *rename*. Rename
`contact_interactions` to `communications` in `schema/tables/` and the diff engine sees one table
gone and one table new, and plans the only thing those two states justify:

```sql
DROP TABLE public.contact_interactions;      -- DELETES_DATA
CREATE TABLE public.communications (…);      -- empty
```

On a dev volume that is a shrug. On an install with a year of correspondence in it, it is the whole
table. And it is worse than a plain failure, because the plan *succeeds* — the operator sees a
`DELETES_DATA` hazard, reasons "yes, I did remove a table", allows it, and the rows are gone.

`data/` cannot fix this: those scripts run *after* the reconcile, by which point the old table no
longer exists.

So there is one more input directory, `premigrate/`, and `apply` runs it first:

| # | Phase | Applied by | Ledgered |
|---|-------|-----------|----------|
| 0 | `premigrate/` | `DataSeedRunner` | yes — `forge_db.data_migration_log` |
| 1 | schema reconcile | pg-schema-diff | no (state, not history) |
| 2 | `data/` then `seed/` | `DataSeedRunner` | yes — same ledger |

Phase 0 runs before the plan is **computed**, not merely before it is applied. The plan is derived
from live DB state; a rename applied after planning would leave the reconcile working against a
shape that no longer exists.

The rename then becomes invisible to the diff — by the time pg-schema-diff looks, the table is
already called `communications`, and the only delta left is the genuinely additive one (the new
columns, the new FKs), which is exactly what the desired-state model is good at.

**Contract** (see [premigrate/README.md](../premigrate/README.md)): numbered, one concern per file,
applied-once via the shared ledger, each in its own transaction, and authored to be idempotent
anyway — `ALTER TABLE IF EXISTS`, a guard around `RENAME COLUMN` (which has no `IF EXISTS` form).
Idempotence is not belt-and-braces here: a script must be safe against a database already at the
target shape, because a fresh install provisioned from `schema/` is exactly that.

**What does not belong here.** Anything pg-schema-diff *can* express. A column added by hand in
`premigrate/` is a column the desired state does not know about, and the very next reconcile will
plan to drop it. The rule of thumb: if the change is describable as a difference between two
states, it goes in `schema/`; if it is only describable as an *action*, it goes here.

**`plan` does not run these scripts** — it stays a pure read. Instead it checks the ledger and warns
when any are pending, because the plan it prints is the one that would run *without* them, hazards
and all. That warning is load-bearing: without it, `plan` shows a `DELETES_DATA` on a table the
pre-migrate script exists to preserve, and the honest-looking response is to reach for
`--allow-destructive`.

> **Status: BUILT.** `SchemaLayout.PreMigrateDir`, phase-parameterised `DataSeedRunner.Discover` /
> `Apply` (so all three phases share one ledger and one applied-once guarantee), phase 0 in
> `ApplyCommand`, and the pending-script warning in `PlanCommand`. First script:
> `premigrate/0010-rename-communications-attestations.sql`, covering the
> `contact_interactions`→`communications` and `sales_order_acceptances`→`attestations` renames from
> the proof-of-intent work.

---

## 7. Decisions (settled)

| # | Decision | Choice |
|---|---|---|
| 1 | **Deploy location** | Deploy-time `apply` in forge-deploy/CI; **boot is read-only** (`verify`, refuse-on-drift). `MigrateAsync()` removed from boot. (§6) |
| 2 | **Diff engine** | **stripe/pg-schema-diff** (MIT, no registration). Declarative-pure: `pg-schema-diff plan` computes the migration from `schema/` vs live; no versioned migration files; audit via `history/` capture (§4.1). *Atlas was the original choice, but its free tier gates `CREATE EXTENSION`/`FUNCTION`/`TRIGGER` behind registration — unacceptable for an OSS self-host stack — so it was dropped.* |
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

---

## 9. Notes from the forge-api squash (the prerequisite — now executed)

The §2 prerequisite (the EF migration squash) is **merged and deployed**: forge-api
**PR armoryworks/forge-api#18** → `main a8260a75`, and the rebaseline deploy succeeded (the boot
reconciler collapsed the live `__EFMigrationsHistory` to the baseline, data intact). The prod
deploy hold otherwise stands.
It collapses 133 migrations into one `InitialBaseline`, proven a schema no-op via the schema-diff
check, and rehearsed end-to-end against the **real armoryworks-api install** (data 100% intact, reconciled
schema identical to a fresh squashed install). That `InitialBaseline` + its `pg_dump` is the canonical
SQL that will **seed forge-db's `schema/` tree** (the `baseline` verb, §4).

Five findings from that work that **forge-db must honor** — several correct or sharpen this design:

1. **A silent diff gap on triggers/functions is catastrophic (critical).** The squash silently
   dropped the `acct_journal_*` immutability triggers + functions (raw plpgsql, not model-derived)
   and the schema-diff check then in use still reported *"Schemas are synced."* The gap was only
   caught by the test suite — a single missed immutability trigger broke ledger append-only.
   pg-schema-diff is a strict improvement here: it **does** diff functions and triggers, so the
   engine now covers them. **But the lesson stands:** `verify`/`plan` still explicitly compare
   `pg_proc` and `pg_trigger` (the `SchemaObjectVerifier`, §4 callout) as belt-and-suspenders,
   because a silent diff gap on these objects is unacceptable for ledger immutability and because
   pg-schema-diff itself flags non-SQL function dependencies as *untrackable*. The trigger-drop
   regression now trips **both** layers (the engine plans the re-create *and* the explicit check
   fails). The one-directional EF drift-check (§5) inherits this: it too covers triggers/functions,
   or it would pass while they drift. forge-db's whole safety rests on the diff being complete.

2. **Triggers/functions are first-class `schema/` objects — and pg-schema-diff diffs them.** EF
   couldn't express them (they lived in a hand-written `migrationBuilder.Sql()` migration). In
   forge-db they belong in `schema/functions/` + `schema/triggers/` as desired-state files —
   exactly the kind of object the declarative model handles better than EF did, and that
   pg-schema-diff natively reconciles (unlike Atlas-free, which wouldn't even process
   `CREATE FUNCTION`/`TRIGGER`). Seed `schema/triggers/acct_journal_*` from the restored
   `RestoreLedgerImmutabilityTriggers` SQL.

3. **Postgres truncates identifiers to 63 chars.** Several FK names exceed 63 chars and are stored
   truncated (e.g. `fk_mrp_planned_orders__purchase_orders_released_purchase_order_id` → 63). Any
   name-based comparison in the harness must match the 63-char truncation, not the authored name.

4. **The deployed schema carries vestigial column defaults.** 66 legacy backfill defaults (bools
   `false`, numerics `0`/`0.0`, enum/string `''`/`'Component'` etc.) live in the deployed schema and
   are reproduced in the baseline → they will appear in `schema/tables/*.sql`. They are vestigial
   (the app always sets values); a future `schema/` cleanup could drop them, but the squash kept them
   so the cutover stays a no-op. Note for whoever curates the seeded `schema/` tree: these defaults
   are intentional-for-now, not authored intent.

5. **Identity FK names use a legacy double-underscore form.** The 4 `asp_net_user_*` → `asp_net_users`
   FKs are named `fk_..._claims__asp_net_users_user_id` (double `_`) in the deployed schema. The
   seeded `schema/` must preserve these exact names (forge-api pins them in `OnModelCreating`), or the
   first forge-db diff will show spurious renames.
