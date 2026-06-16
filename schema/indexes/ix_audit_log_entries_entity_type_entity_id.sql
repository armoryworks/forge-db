CREATE INDEX ix_audit_log_entries_entity_type_entity_id ON public.audit_log_entries USING btree (entity_type, entity_id);
