CREATE UNIQUE INDEX ix_user_scan_identifiers_identifier_type_identifier_value ON public.user_scan_identifiers USING btree (identifier_type, identifier_value) WHERE (deleted_at IS NULL);
