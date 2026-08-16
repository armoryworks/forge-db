CREATE INDEX ix_communications_occurred_at ON public.communications USING btree (occurred_at) WHERE (party_id IS NULL AND deleted_at IS NULL);
