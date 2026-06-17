CREATE INDEX ix_file_attachments_entity_type_entity_id ON public.file_attachments USING btree (entity_type, entity_id);
