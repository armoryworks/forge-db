CREATE UNIQUE INDEX ix_user_devices_device_token_hash ON public.user_devices USING btree (device_token_hash);
