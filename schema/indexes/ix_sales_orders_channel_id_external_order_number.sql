CREATE INDEX ix_sales_orders_channel_id_external_order_number ON public.sales_orders USING btree (channel_id, external_order_number) WHERE (external_order_number IS NOT NULL);
