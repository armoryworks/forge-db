CREATE INDEX ix_sequence_step_instances_dwell_expires_at ON public.sequence_step_instances USING btree (dwell_expires_at) WHERE ((dwell_fired_at IS NULL) AND (dwell_expires_at IS NOT NULL));
