CREATE UNIQUE INDEX ix_channel_listings_channel_id_external_listing_id ON public.channel_listings USING btree (channel_id, external_listing_id);
