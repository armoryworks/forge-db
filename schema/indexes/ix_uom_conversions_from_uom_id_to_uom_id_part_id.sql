CREATE UNIQUE INDEX ix_uom_conversions_from_uom_id_to_uom_id_part_id ON public.uom_conversions USING btree (from_uom_id, to_uom_id, part_id);
