CREATE UNIQUE INDEX ix_sales_orders_order_number ON public.sales_orders USING btree (order_number);
