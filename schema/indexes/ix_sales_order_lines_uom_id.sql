CREATE INDEX ix_sales_order_lines_uom_id ON public.sales_order_lines USING btree (uom_id);
