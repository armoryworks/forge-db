CREATE UNIQUE INDEX ix_sales_orders_quote_id ON public.sales_orders USING btree (quote_id);
