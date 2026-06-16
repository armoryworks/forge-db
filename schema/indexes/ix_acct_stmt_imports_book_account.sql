CREATE INDEX ix_acct_stmt_imports_book_account ON public.acct_bank_statement_imports USING btree (book_id, cash_gl_account_id);
