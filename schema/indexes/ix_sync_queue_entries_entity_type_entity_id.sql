CREATE INDEX ix_sync_queue_entries_entity_type_entity_id ON public.sync_queue_entries USING btree (entity_type, entity_id);
