CREATE UNIQUE INDEX "ix_entity_capability_requirements_entity_type_capability_code_~" ON public.entity_capability_requirements USING btree (entity_type, capability_code, requirement_id);
