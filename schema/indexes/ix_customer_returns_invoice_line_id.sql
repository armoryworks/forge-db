CREATE INDEX ix_customer_returns_invoice_line_id ON public.customer_returns USING btree (invoice_line_id);
