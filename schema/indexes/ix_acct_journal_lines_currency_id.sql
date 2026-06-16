CREATE INDEX ix_acct_journal_lines_currency_id ON public.acct_journal_lines USING btree (currency_id);
