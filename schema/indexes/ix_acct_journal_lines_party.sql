CREATE INDEX ix_acct_journal_lines_party ON public.acct_journal_lines USING btree (subledger_party_type, subledger_party_id);
