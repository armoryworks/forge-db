CREATE UNIQUE INDEX ux_acct_budgets_book_account_year_period ON public.acct_budgets USING btree (book_id, gl_account_id, fiscal_year, period_month) WHERE (deleted_at IS NULL);
