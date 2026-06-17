CREATE INDEX ix_auto_po_suggestions_part_id_status ON public.auto_po_suggestions USING btree (part_id, status);
