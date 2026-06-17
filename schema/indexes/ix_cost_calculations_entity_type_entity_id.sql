CREATE INDEX ix_cost_calculations_entity_type_entity_id ON public.cost_calculations USING btree (entity_type, entity_id);
