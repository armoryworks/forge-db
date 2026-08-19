CREATE UNIQUE INDEX ix_sequence_step_instances_instance_id_step_key ON public.sequence_step_instances USING btree (instance_id, step_key);
