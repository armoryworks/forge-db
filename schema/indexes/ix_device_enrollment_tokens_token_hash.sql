CREATE UNIQUE INDEX ix_device_enrollment_tokens_token_hash ON public.device_enrollment_tokens USING btree (token_hash);
