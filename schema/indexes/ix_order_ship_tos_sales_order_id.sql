CREATE UNIQUE INDEX ix_order_ship_tos_sales_order_id ON public.order_ship_tos USING btree (sales_order_id);
