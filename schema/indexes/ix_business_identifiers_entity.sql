CREATE INDEX ix_business_identifiers_entity ON public.business_identifiers USING btree (entity_type, entity_id);
