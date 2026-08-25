CREATE UNIQUE INDEX ix_user_sessions_jti ON public.user_sessions USING btree (jti);
