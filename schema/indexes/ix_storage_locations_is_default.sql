CREATE UNIQUE INDEX ix_storage_locations_is_default ON public.storage_locations USING btree (is_default) WHERE ((is_default = true) AND (deleted_at IS NULL));
