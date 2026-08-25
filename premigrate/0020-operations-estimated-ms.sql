-- 0020-operations-estimated-ms.sql
--
-- operations.estimated_minutes (integer, minutes) -> operations.estimated_ms (bigint, milliseconds)
--
-- Sub-second operation times: the routing editor now composes an estimate from Hours / Minutes /
-- Milliseconds and stores it canonically in milliseconds. Minutes cannot round-trip a millisecond
-- value, so the column is both renamed and widened, and every existing value is converted
-- (minutes × 60000) in place.
--
-- This runs BEFORE the schema reconcile. pg-schema-diff has no concept of a rename: without this it
-- would see estimated_minutes vanish and estimated_ms appear, and plan DROP + ADD COLUMN — which
-- discards every stored estimate. Renaming here first means the reconcile only sees a type change on
-- an already-correctly-named column (bigint, already holding ms), leaving nothing to do.
--
-- Idempotent: the whole body is guarded on estimated_minutes still existing. On a fresh install the
-- column is created as estimated_ms directly (from schema/), so nothing matches; on a second deploy
-- the rename already happened, so nothing matches. Either way this is a no-op.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'operations'
          AND column_name = 'estimated_minutes'
    ) THEN
        ALTER TABLE public.operations RENAME COLUMN estimated_minutes TO estimated_ms;
        ALTER TABLE public.operations
            ALTER COLUMN estimated_ms TYPE bigint USING (estimated_ms::bigint * 60000);
    END IF;
END $$;
