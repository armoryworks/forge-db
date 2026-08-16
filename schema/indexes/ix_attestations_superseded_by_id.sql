CREATE INDEX ix_attestations_superseded_by_id ON public.attestations USING btree (superseded_by_id);
