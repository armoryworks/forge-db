CREATE UNIQUE INDEX ix_user_cloud_storage_links_user_id_provider_id ON public.user_cloud_storage_links USING btree (user_id, provider_id) WHERE (deleted_at IS NULL);
