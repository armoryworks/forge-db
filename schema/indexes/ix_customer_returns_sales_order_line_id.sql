CREATE INDEX ix_customer_returns_sales_order_line_id ON public.customer_returns USING btree (sales_order_line_id);
