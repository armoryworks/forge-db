CREATE INDEX ix_workflow_run_entities_entity_type_entity_id ON public.workflow_run_entities USING btree (entity_type, entity_id);
