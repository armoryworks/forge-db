CREATE INDEX ix_payment_transmissions_source ON public.payment_transmissions USING btree (source_type, source_id);
