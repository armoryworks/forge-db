CREATE UNIQUE INDEX ix_customer_portal_accesses_contact_id ON public.customer_portal_accesses USING btree (contact_id) WHERE (deleted_at IS NULL);
