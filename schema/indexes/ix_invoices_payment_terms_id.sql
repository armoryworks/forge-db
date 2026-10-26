CREATE INDEX ix_invoices_payment_terms_id ON public.invoices USING btree (payment_terms_id);
