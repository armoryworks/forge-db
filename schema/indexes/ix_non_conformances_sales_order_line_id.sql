CREATE INDEX ix_non_conformances_sales_order_line_id ON public.non_conformances USING btree (sales_order_line_id);
