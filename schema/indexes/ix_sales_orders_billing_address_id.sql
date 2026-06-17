CREATE INDEX ix_sales_orders_billing_address_id ON public.sales_orders USING btree (billing_address_id);
