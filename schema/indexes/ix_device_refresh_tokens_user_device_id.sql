CREATE INDEX ix_device_refresh_tokens_user_device_id ON public.device_refresh_tokens USING btree (user_device_id);
