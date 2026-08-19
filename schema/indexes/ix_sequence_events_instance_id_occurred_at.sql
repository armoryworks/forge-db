CREATE INDEX ix_sequence_events_instance_id_occurred_at ON public.sequence_events USING btree (instance_id, occurred_at);
