CREATE INDEX ix_idempotency_keys_expires_at ON public.idempotency_keys USING btree (expires_at);
