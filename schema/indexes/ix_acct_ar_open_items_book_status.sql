CREATE INDEX ix_acct_ar_open_items_book_status ON public.acct_ar_open_items USING btree (book_id, status);
