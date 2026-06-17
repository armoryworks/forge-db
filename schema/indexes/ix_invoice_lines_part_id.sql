CREATE INDEX ix_invoice_lines_part_id ON public.invoice_lines USING btree (part_id);
