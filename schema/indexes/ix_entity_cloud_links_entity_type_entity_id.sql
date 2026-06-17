CREATE INDEX ix_entity_cloud_links_entity_type_entity_id ON public.entity_cloud_links USING btree (entity_type, entity_id);
