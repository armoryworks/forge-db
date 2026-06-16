CREATE INDEX ix_oauth_state_tokens_user_id ON public.oauth_state_tokens USING btree (user_id);
