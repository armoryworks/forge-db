CREATE INDEX ix_communication_sync_configs_user_id_kind_provider_id ON public.communication_sync_configs USING btree (user_id, kind, provider_id);
