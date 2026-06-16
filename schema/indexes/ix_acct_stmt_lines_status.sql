CREATE INDEX ix_acct_stmt_lines_status ON public.acct_bank_statement_lines USING btree (match_status);
