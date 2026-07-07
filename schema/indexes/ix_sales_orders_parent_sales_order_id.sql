CREATE INDEX ix_sales_orders_parent_sales_order_id ON public.sales_orders USING btree (parent_sales_order_id);
