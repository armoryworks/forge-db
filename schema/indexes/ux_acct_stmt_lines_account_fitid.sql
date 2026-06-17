CREATE UNIQUE INDEX ux_acct_stmt_lines_account_fitid ON public.acct_bank_statement_lines USING btree (cash_gl_account_id, fitid);
