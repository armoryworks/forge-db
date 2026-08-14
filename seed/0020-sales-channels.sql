-- 0020-sales-channels.sql — the install's default sales channel, and the backfill that points
-- pre-channel orders at it.
--
-- Why this is reference data rather than a pure backfill: sales_orders.channel_id is nullable and
-- resolves null to the single is_default channel (the same "null = the default row" convention
-- ApplicationUser.WorkLocationId uses against company_locations). The application therefore ASSUMES
-- exactly one default channel exists — a filtered unique index (ix_sales_channels_is_default)
-- enforces at-most-one, and this script guarantees at-least-one.
--
-- The backfill lives here rather than in data/ because data/ runs BEFORE seed/, and the UPDATE
-- depends on the row this script inserts. One concern — "establish the default channel" — so one
-- file.
--
-- DirectB2B + TaxCollectedBy=Seller is deliberately the pre-channel behaviour: every order that
-- existed before channels was an account order on which the install collected its own sales tax.
-- Backfilling to anything else would retroactively change tax liability on closed periods.
--
-- Applied-once (forge_db.data_migration_log) AND idempotent: NOT EXISTS on the insert, and the
-- update only touches rows still carrying NULL.

INSERT INTO sales_channels (
    name, code, description, channel_type, tax_collected_by,
    is_default, is_active, created_at, updated_at)
SELECT
    'Direct', 'DIRECT',
    'Default channel for account business booked directly rather than through a storefront or marketplace.',
    'DirectB2B', 'Seller',
    true, true, now(), now()
WHERE NOT EXISTS (SELECT 1 FROM sales_channels WHERE code = 'DIRECT');

UPDATE sales_orders so
SET channel_id = sc.id
FROM sales_channels sc
WHERE sc.code = 'DIRECT'
  AND so.channel_id IS NULL;
