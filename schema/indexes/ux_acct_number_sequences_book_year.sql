CREATE UNIQUE INDEX ux_acct_number_sequences_book_year ON public.acct_number_sequences USING btree (book_id, fiscal_year_id);
