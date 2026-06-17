CREATE INDEX ix_scan_action_logs_part_id ON public.scan_action_logs USING btree (part_id) WHERE (part_id IS NOT NULL);
