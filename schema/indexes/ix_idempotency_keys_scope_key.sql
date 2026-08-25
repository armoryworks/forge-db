CREATE UNIQUE INDEX ix_idempotency_keys_scope_key ON public.idempotency_keys USING btree (scope, idempotency_key);
