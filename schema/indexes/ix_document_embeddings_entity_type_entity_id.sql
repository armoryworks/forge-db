CREATE INDEX ix_document_embeddings_entity_type_entity_id ON public.document_embeddings USING btree (entity_type, entity_id);
