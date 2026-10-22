CREATE INDEX ix_invoice_lines_sales_order_line_id ON public.invoice_lines USING btree (sales_order_line_id);
