CREATE UNIQUE INDEX ix_user_integrations_user_id_provider_id ON public.user_integrations USING btree (user_id, provider_id) WHERE (deleted_at IS NULL);
