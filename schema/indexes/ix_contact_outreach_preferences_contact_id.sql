CREATE UNIQUE INDEX ix_contact_outreach_preferences_contact_id ON public.contact_outreach_preferences USING btree (contact_id) WHERE (deleted_at IS NULL);
