CREATE INDEX ix_attestations_party_type_party_id ON public.attestations USING btree (party_type, party_id);
