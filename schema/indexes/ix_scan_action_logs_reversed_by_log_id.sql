CREATE INDEX ix_scan_action_logs_reversed_by_log_id ON public.scan_action_logs USING btree (reversed_by_log_id) WHERE (reversed_by_log_id IS NOT NULL);
