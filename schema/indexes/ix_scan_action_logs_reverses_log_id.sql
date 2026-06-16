CREATE INDEX ix_scan_action_logs_reverses_log_id ON public.scan_action_logs USING btree (reverses_log_id) WHERE (reverses_log_id IS NOT NULL);
