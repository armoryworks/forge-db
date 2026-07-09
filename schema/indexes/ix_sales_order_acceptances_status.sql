CREATE INDEX ix_sales_order_acceptances_status ON public.sales_order_acceptances USING btree (sales_order_id, status);
