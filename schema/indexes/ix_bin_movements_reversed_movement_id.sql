CREATE INDEX ix_bin_movements_reversed_movement_id ON public.bin_movements USING btree (reversed_movement_id) WHERE (reversed_movement_id IS NOT NULL);
