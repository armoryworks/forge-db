CREATE UNIQUE INDEX ix_leads_converted_customer_id ON public.leads USING btree (converted_customer_id);
