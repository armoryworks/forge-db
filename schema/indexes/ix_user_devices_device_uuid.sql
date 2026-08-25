CREATE UNIQUE INDEX ix_user_devices_device_uuid ON public.user_devices USING btree (device_uuid);
