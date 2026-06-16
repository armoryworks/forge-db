CREATE INDEX ix_status_entries_entity_type_entity_id_ended_at ON public.status_entries USING btree (entity_type, entity_id, ended_at);
