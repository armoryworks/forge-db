CREATE UNIQUE INDEX ix_business_identifiers_entity_active ON public.business_identifiers USING btree (entity_type, entity_id) WHERE is_active;
