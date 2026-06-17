CREATE INDEX ix_integration_outbox_entries_entity ON public.integration_outbox_entries USING btree (entity_type, entity_id);
