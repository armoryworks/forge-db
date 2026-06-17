CREATE UNIQUE INDEX ix_pay_stubs_external_id ON public.pay_stubs USING btree (external_id) WHERE (external_id IS NOT NULL);
