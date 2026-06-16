CREATE INDEX ix_ecommerce_order_syncs_sales_order_id ON public.ecommerce_order_syncs USING btree (sales_order_id);
