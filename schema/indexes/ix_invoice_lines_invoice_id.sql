CREATE INDEX ix_invoice_lines_invoice_id ON public.invoice_lines USING btree (invoice_id);
