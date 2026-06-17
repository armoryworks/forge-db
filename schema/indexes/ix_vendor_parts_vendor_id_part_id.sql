CREATE UNIQUE INDEX ix_vendor_parts_vendor_id_part_id ON public.vendor_parts USING btree (vendor_id, part_id);
