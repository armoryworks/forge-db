CREATE INDEX ix_scan_action_logs_to_location_id ON public.scan_action_logs USING btree (to_location_id);
