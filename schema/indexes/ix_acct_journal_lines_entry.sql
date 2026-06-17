CREATE INDEX ix_acct_journal_lines_entry ON public.acct_journal_lines USING btree (journal_entry_id);
