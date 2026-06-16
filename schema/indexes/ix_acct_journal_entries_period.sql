CREATE INDEX ix_acct_journal_entries_period ON public.acct_journal_entries USING btree (fiscal_period_id);
