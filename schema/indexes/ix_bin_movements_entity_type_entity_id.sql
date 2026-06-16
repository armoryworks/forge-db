CREATE INDEX ix_bin_movements_entity_type_entity_id ON public.bin_movements USING btree (entity_type, entity_id);
