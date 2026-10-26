CREATE INDEX ix_sales_orders_payment_terms_id ON public.sales_orders USING btree (payment_terms_id);
