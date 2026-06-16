CREATE INDEX ix_sales_orders_shipping_address_id ON public.sales_orders USING btree (shipping_address_id);
