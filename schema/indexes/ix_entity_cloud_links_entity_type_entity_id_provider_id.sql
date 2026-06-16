CREATE UNIQUE INDEX ix_entity_cloud_links_entity_type_entity_id_provider_id ON public.entity_cloud_links USING btree (entity_type, entity_id, provider_id) WHERE (deleted_at IS NULL);
