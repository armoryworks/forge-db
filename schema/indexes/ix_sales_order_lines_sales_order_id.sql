CREATE INDEX ix_sales_order_lines_sales_order_id ON public.sales_order_lines USING btree (sales_order_id);
