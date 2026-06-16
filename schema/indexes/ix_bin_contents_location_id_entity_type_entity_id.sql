CREATE INDEX ix_bin_contents_location_id_entity_type_entity_id ON public.bin_contents USING btree (location_id, entity_type, entity_id);
