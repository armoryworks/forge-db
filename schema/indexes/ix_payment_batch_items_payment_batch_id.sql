CREATE INDEX ix_payment_batch_items_payment_batch_id ON public.payment_batch_items USING btree (payment_batch_id);
