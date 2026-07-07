CREATE INDEX ix_payment_milestones_invoice_id ON public.payment_milestones USING btree (invoice_id);
