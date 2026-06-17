CREATE UNIQUE INDEX ix_workflow_runs_entity_type_entity_id ON public.workflow_runs USING btree (entity_type, entity_id) WHERE (entity_id IS NOT NULL);
