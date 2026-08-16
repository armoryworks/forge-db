CREATE INDEX ix_communications_party_type_party_id_occurred_at ON public.communications USING btree (party_type, party_id, occurred_at);
