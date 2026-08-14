CREATE INDEX ix_channel_listings_channel_id_external_sku ON public.channel_listings USING btree (channel_id, external_sku);
