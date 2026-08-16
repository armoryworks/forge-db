CREATE UNIQUE INDEX ix_communication_links_communication_id_entity_type_entity_id ON public.communication_links USING btree (communication_id, entity_type, entity_id) WHERE (deleted_at IS NULL);
