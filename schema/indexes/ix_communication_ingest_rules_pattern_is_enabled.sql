CREATE INDEX ix_communication_ingest_rules_pattern_is_enabled ON public.communication_ingest_rules USING btree (pattern, is_enabled);
