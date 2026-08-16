CREATE UNIQUE INDEX ix_communication_ingest_rules_match_type_pattern ON public.communication_ingest_rules USING btree (match_type, pattern) WHERE (deleted_at IS NULL);
