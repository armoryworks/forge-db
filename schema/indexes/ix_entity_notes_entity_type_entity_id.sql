CREATE INDEX ix_entity_notes_entity_type_entity_id ON public.entity_notes USING btree (entity_type, entity_id);
