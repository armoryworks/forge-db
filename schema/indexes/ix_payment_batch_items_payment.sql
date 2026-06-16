CREATE INDEX ix_payment_batch_items_payment ON public.payment_batch_items USING btree (vendor_payment_id);
