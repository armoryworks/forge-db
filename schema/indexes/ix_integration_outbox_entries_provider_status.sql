CREATE INDEX ix_integration_outbox_entries_provider_status ON public.integration_outbox_entries USING btree (provider, status);
