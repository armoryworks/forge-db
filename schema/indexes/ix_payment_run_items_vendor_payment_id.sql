CREATE INDEX ix_payment_run_items_vendor_payment_id ON public.payment_run_items USING btree (vendor_payment_id);
