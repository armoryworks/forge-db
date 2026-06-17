CREATE UNIQUE INDEX ix_lead_outreach_preferences_lead_id ON public.lead_outreach_preferences USING btree (lead_id) WHERE (deleted_at IS NULL);
