-- GIN trigram index on parts.name, powering the near-duplicate part-name guard
-- (pg_trgm similarity() lookups). Partial on deleted_at IS NULL to match the
-- soft-delete query filter (same pattern as ux_parts_gtin).
CREATE INDEX ix_parts_name_trgm ON public.parts USING gin (name gin_trgm_ops) WHERE (deleted_at IS NULL);
