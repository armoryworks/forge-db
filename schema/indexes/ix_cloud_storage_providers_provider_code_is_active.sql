CREATE UNIQUE INDEX ix_cloud_storage_providers_provider_code_is_active ON public.cloud_storage_providers USING btree (provider_code, is_active) WHERE ((is_active = true) AND (deleted_at IS NULL));
