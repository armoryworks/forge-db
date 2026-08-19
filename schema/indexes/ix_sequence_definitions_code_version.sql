CREATE UNIQUE INDEX ix_sequence_definitions_code_version ON public.sequence_definitions USING btree (code, version) WHERE (deleted_at IS NULL);
