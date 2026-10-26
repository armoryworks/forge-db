CREATE INDEX ix_customer_returns_invoice_id ON public.customer_returns USING btree (invoice_id);
