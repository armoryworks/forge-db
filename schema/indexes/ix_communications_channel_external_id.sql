CREATE UNIQUE INDEX ix_communications_channel_external_id ON public.communications USING btree (channel, external_id) WHERE (external_id IS NOT NULL);
