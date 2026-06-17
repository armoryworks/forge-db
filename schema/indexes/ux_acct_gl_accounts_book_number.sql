CREATE UNIQUE INDEX ux_acct_gl_accounts_book_number ON public.acct_gl_accounts USING btree (book_id, account_number);
