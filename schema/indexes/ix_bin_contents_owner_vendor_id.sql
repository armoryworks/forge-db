CREATE INDEX ix_bin_contents_owner_vendor_id ON public.bin_contents USING btree (owner_vendor_id) WHERE (owner_vendor_id IS NOT NULL);
