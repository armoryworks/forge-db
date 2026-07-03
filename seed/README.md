# seed/ — schema-adjacent reference data (INPUT)

Reference / lookup rows that are part of the schema's *meaning* rather than transactional data —
e.g. `reference_data` groups whose presence the application assumes (job priorities, contact roles,
UoM, carriers, currencies, the chart-of-accounts reference, compliance taxonomy, …).

Applied by `DataSeedRunner` during `forge-db apply`, after `data/` and after the schema reconcile,
under the same ordered + applied-once + idempotent-anyway convention — see
[../data/README.md](../data/README.md#authoring-convention-applies-to-data-and-seed).

Idempotent upserts should key on each table's **stable natural key** (e.g. `reference_data
(group_code, code)`, and set `is_seed_data = true` on `reference_data` rows), so a script is safe to
re-run and so ownership can transfer from the forge-api boot seeders without double-seeding.

Empty until the forge-api reference seeders are ported (see the seed-migration effort).
