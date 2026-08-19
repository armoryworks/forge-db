CREATE UNIQUE INDEX ix_sequence_gate_definitions_definition_id_step_key_key ON public.sequence_gate_definitions USING btree (definition_id, step_key, key);
