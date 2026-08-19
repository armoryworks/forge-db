CREATE UNIQUE INDEX ix_sequence_gate_instances_instance_id_step_key_gate_key ON public.sequence_gate_instances USING btree (instance_id, step_key, gate_key);
