CREATE INDEX ix_user_mfa_devices_user_id ON public.user_mfa_devices USING btree (user_id);
