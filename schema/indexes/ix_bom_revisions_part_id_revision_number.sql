CREATE UNIQUE INDEX ix_bom_revisions_part_id_revision_number ON public.bom_revisions USING btree (part_id, revision_number);
