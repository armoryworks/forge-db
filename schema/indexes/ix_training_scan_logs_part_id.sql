CREATE INDEX ix_training_scan_logs_part_id ON public.training_scan_logs USING btree (part_id) WHERE (part_id IS NOT NULL);
