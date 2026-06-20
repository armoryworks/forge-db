CREATE INDEX ix_document_set_links_entity ON public.document_set_links USING btree (entity_type, entity_id);
