CREATE UNIQUE INDEX ux_acct_bank_rec_items_rec_line ON public.acct_bank_reconciliation_items USING btree (bank_reconciliation_id, journal_line_id);
