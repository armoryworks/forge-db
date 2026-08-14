CREATE INDEX ix_retail_buyers_purge_after ON public.retail_buyers USING btree (purge_after) WHERE (purge_after IS NOT NULL);
