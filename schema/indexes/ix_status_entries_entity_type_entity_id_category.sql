CREATE INDEX ix_status_entries_entity_type_entity_id_category ON public.status_entries USING btree (entity_type, entity_id, category);
