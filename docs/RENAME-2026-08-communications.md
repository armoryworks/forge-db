# Manual step: the communications / attestations rename

`contact_interactions` → `communications` and `sales_order_acceptances` → `attestations`.

## Why this needs a hand

forge-db is desired-state. pg-schema-diff has no concept of a rename — it sees a table that is
gone and a table that is new, and plans `DROP` + `CREATE`. Applying that to a populated database
destroys every row in both tables.

`data/` scripts cannot cover it either: `DataSeedRunner` runs **after** the schema reconcile, by
which point the old tables are already gone.

## Fresh installs

Nothing to do. `SchemaBootstrapper` applies the embedded schema to an empty database and the new
names are what it creates.

## Populated installs

Run the rename **before** `forge-db apply`, so the reconcile sees tables that already have the
right names and plans only `ADD COLUMN` / `ALTER COLUMN DROP NOT NULL`:

```sql
ALTER TABLE IF EXISTS public.contact_interactions      RENAME TO communications;
ALTER TABLE IF EXISTS public.sales_order_acceptances   RENAME TO attestations;

-- Column renames within them.
ALTER TABLE public.communications RENAME COLUMN user_id          TO handled_by_user_id;
ALTER TABLE public.communications RENAME COLUMN interaction_date TO occurred_at;
```

Then apply as usual:

```bash
forge-db plan  --db "$DB"                      # expect only ADD COLUMN / DROP NOT NULL
forge-db apply --db "$DB" --env dev --yes
```

`IF EXISTS` makes the script safe to re-run and safe on an install that has already been renamed.

Indexes, the primary key and foreign-key constraints follow a `RENAME TO` automatically in
Postgres, but they keep their **old names** (`pk_contact_interactions`, and so on). pg-schema-diff
will then plan to drop and recreate them under the new names. That is non-destructive — an index
rebuild, not data loss — so it is fine to let it. On a large table, do it in a maintenance window.

## Verify

```sql
SELECT count(*) FROM communications;   -- matches the pre-rename contact_interactions count
SELECT count(*) FROM attestations;     -- matches the pre-rename sales_order_acceptances count
```

## Alternative: clean rebuild

`forge-db dump` / `import` (DESIGN §6.2) is the other route, but `import` matches dump files to
target tables **by name** — renamed tables land in `MissingInTarget` and their rows are silently
dropped. If you go that way, rename the dump files before importing. The `ALTER TABLE` above is
simpler and has no silent-loss mode.
