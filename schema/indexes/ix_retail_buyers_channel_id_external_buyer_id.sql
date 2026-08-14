CREATE UNIQUE INDEX ix_retail_buyers_channel_id_external_buyer_id ON public.retail_buyers USING btree (channel_id, external_buyer_id);
