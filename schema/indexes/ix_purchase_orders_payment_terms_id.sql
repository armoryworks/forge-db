CREATE INDEX ix_purchase_orders_payment_terms_id ON public.purchase_orders USING btree (payment_terms_id);
