CREATE UNIQUE INDEX ux_acct_ledger_balances_grain ON public.acct_ledger_balances USING btree (book_id, gl_account_id, fiscal_period_id, currency_id);
