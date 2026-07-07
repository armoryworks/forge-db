CREATE INDEX ix_sales_order_stages_sales_order_id ON public.sales_order_stages USING btree (sales_order_id);
