CREATE UNIQUE INDEX ix_sequence_step_definitions_definition_id_key ON public.sequence_step_definitions USING btree (definition_id, key);
