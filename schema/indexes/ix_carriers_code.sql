CREATE UNIQUE INDEX ix_carriers_code ON public.carriers USING btree (code) WHERE (code IS NOT NULL);
