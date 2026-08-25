CREATE INDEX ix_user_sessions_expires_at ON public.user_sessions USING btree (expires_at);
