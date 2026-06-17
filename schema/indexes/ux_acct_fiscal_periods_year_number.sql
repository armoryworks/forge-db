CREATE UNIQUE INDEX ux_acct_fiscal_periods_year_number ON public.acct_fiscal_periods USING btree (fiscal_year_id, period_number);
