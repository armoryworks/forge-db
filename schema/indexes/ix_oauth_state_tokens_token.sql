CREATE UNIQUE INDEX ix_oauth_state_tokens_token ON public.oauth_state_tokens USING btree (token);
