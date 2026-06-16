CREATE INDEX ix_reorder_suggestions_part_id_status ON public.reorder_suggestions USING btree (part_id, status);
