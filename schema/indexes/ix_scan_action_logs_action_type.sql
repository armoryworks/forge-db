CREATE INDEX ix_scan_action_logs_action_type ON public.scan_action_logs USING btree (action_type);
