CREATE INDEX ix_activity_logs_entity_type_entity_id ON public.activity_logs USING btree (entity_type, entity_id);
