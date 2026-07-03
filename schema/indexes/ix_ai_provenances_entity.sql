CREATE UNIQUE INDEX ix_ai_provenances_entity ON public.ai_provenances USING btree (entity_type, entity_id);
