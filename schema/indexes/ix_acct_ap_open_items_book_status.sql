CREATE INDEX ix_acct_ap_open_items_book_status ON public.acct_ap_open_items USING btree (book_id, status);
