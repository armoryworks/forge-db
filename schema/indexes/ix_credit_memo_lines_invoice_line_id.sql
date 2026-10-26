CREATE INDEX ix_credit_memo_lines_invoice_line_id ON public.credit_memo_lines USING btree (invoice_line_id);
