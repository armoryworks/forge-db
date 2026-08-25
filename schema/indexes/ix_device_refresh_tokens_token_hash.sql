CREATE UNIQUE INDEX ix_device_refresh_tokens_token_hash ON public.device_refresh_tokens USING btree (token_hash);
