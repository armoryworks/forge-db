CREATE INDEX ix_scan_action_logs_from_location_id ON public.scan_action_logs USING btree (from_location_id);
