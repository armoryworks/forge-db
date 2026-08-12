CREATE UNIQUE INDEX ix_leads_external_id ON public.leads USING btree (external_id) WHERE (deleted_at IS NULL);
