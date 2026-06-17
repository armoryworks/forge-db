CREATE INDEX ix_acct_bank_recs_book_account_date ON public.acct_bank_reconciliations USING btree (book_id, cash_gl_account_id, statement_date);
