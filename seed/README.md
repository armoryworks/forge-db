# seed/ — schema-adjacent reference data (INPUT)

Reference / lookup rows that are part of the schema's *meaning* rather than transactional data —
e.g. `reference_data` groups whose presence the application assumes. Applied like `data/` backfills
(idempotent, applied-once). Empty until needed.
