CREATE INDEX ix_acct_journal_lines_cost_center_id ON public.acct_journal_lines USING btree (cost_center_id);
