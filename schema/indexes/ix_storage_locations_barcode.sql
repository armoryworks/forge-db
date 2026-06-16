CREATE UNIQUE INDEX ix_storage_locations_barcode ON public.storage_locations USING btree (barcode) WHERE (barcode IS NOT NULL);
