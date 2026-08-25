CREATE INDEX ix_user_sessions_user_device_id ON public.user_sessions USING btree (user_device_id);
