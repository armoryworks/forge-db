CREATE INDEX ix_approval_requests_entity_type_entity_id ON public.approval_requests USING btree (entity_type, entity_id);
