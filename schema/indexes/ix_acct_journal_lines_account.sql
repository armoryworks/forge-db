CREATE INDEX ix_acct_journal_lines_account ON public.acct_journal_lines USING btree (gl_account_id);
