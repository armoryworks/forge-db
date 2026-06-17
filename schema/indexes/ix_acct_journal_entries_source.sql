CREATE INDEX ix_acct_journal_entries_source ON public.acct_journal_entries USING btree (source_type, source_id);
