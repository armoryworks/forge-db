CREATE INDEX ix_invoices_sales_tax_rate_id ON public.invoices USING btree (sales_tax_rate_id);
