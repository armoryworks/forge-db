CREATE INDEX ix_communication_links_party_part ON public.communication_links USING btree (party_id, entity_id) WHERE (entity_type = 'Part');
