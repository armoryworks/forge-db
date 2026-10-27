CREATE INDEX ix_physical_inventory_tags_bin_movement_id ON public.physical_inventory_tags USING btree (bin_movement_id) WHERE (bin_movement_id IS NOT NULL);
