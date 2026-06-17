CREATE UNIQUE INDEX ux_acct_fiscal_years_book_name ON public.acct_fiscal_years USING btree (book_id, name);
