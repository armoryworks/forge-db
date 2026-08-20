CREATE UNIQUE INDEX ix_leads_lead_number ON public.leads USING btree (lead_number) WHERE lead_number IS NOT NULL;
