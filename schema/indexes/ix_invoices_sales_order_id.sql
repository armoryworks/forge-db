CREATE INDEX ix_invoices_sales_order_id ON public.invoices USING btree (sales_order_id);
