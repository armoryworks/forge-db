CREATE INDEX ix_bin_movements_to_location_id ON public.bin_movements USING btree (to_location_id);
