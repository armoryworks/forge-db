CREATE INDEX ix_bomlines_vendor_id ON public.bomlines USING btree (vendor_id) WHERE (vendor_id IS NOT NULL);
