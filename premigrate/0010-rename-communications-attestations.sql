-- 0010-rename-communications-attestations.sql
--
-- contact_interactions   -> communications
-- sales_order_acceptances -> attestations
--
-- Both tables were generalized rather than replaced: Communication now covers any party and any
-- channel (the old shape could only hang off a Contact), and Attestation now covers any statement a
-- party made (the old shape could only mean "this order was accepted"). The rows are the same rows;
-- only the names and two column names changed.
--
-- This runs BEFORE the schema reconcile. Without it pg-schema-diff sees two tables that vanished and
-- five that appeared, and plans DROP + CREATE — which would delete every interaction and every
-- acceptance record on the target.
--
-- Idempotent: IF EXISTS throughout, and the column renames are guarded on the old name still being
-- present. Safe on a fresh install (nothing matches, nothing happens) and safe on a second deploy.
--
-- Postgres carries indexes, primary keys and foreign keys through a RENAME automatically, but they
-- keep their OLD names. pg-schema-diff then plans to drop and recreate them under the new ones. That
-- is an index rebuild, not data loss, so it is fine to let it happen — on a large table, do it in a
-- maintenance window.

ALTER TABLE IF EXISTS public.contact_interactions    RENAME TO communications;
ALTER TABLE IF EXISTS public.sales_order_acceptances RENAME TO attestations;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'communications' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE public.communications RENAME COLUMN user_id TO handled_by_user_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'communications' AND column_name = 'interaction_date'
    ) THEN
        ALTER TABLE public.communications RENAME COLUMN interaction_date TO occurred_at;
    END IF;
END $$;
