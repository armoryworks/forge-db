CREATE UNIQUE INDEX ux_acct_journal_entries_book_year_num ON public.acct_journal_entries USING btree (book_id, fiscal_year_id, entry_number);
