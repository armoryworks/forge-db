CREATE UNIQUE INDEX ix_part_alternates_part_id_alternate_part_id ON public.part_alternates USING btree (part_id, alternate_part_id);
