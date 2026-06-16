CREATE INDEX ix_deliverables_customer_id ON public.deliverables USING btree (customer_id) WHERE (deleted_at IS NULL);
