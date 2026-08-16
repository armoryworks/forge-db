CREATE INDEX ix_communication_links_entity_type_entity_id ON public.communication_links USING btree (entity_type, entity_id);
