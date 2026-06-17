CREATE UNIQUE INDEX ux_acct_cost_centers_book_code ON public.acct_cost_centers USING btree (book_id, code);
