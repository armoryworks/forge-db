CREATE INDEX ix_bi_api_keys_key_prefix ON public.bi_api_keys USING btree (key_prefix);
