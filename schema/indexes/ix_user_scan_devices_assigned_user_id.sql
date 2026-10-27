CREATE INDEX ix_user_scan_devices_assigned_user_id ON public.user_scan_devices USING btree (assigned_user_id);
