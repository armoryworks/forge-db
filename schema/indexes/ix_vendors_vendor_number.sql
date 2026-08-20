CREATE UNIQUE INDEX ix_vendors_vendor_number ON public.vendors USING btree (vendor_number) WHERE vendor_number IS NOT NULL;
