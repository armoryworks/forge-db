CREATE INDEX ix_leads_assigned_to_user_id ON public.leads USING btree (assigned_to_user_id);
