CREATE INDEX ix_sequence_resource_clocks_expires_at ON public.sequence_resource_clocks USING btree (expires_at) WHERE ((fired_at IS NULL) AND (deleted_at IS NULL));
