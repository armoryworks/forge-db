CREATE UNIQUE INDEX ix_part_revisions_part_id_revision ON public.part_revisions USING btree (part_id, revision);
