CREATE INDEX ix_bin_movements_physical_inventory_id ON public.bin_movements USING btree (physical_inventory_id) WHERE (physical_inventory_id IS NOT NULL);
