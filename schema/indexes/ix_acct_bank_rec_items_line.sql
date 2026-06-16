CREATE INDEX ix_acct_bank_rec_items_line ON public.acct_bank_reconciliation_items USING btree (journal_line_id);
