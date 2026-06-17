CREATE UNIQUE INDEX ix_user_scan_devices_device_id ON public.user_scan_devices USING btree (device_id);
