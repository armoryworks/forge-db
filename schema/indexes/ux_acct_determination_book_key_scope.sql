CREATE UNIQUE INDEX ux_acct_determination_book_key_scope ON public.acct_account_determination_rules USING btree (book_id, key, item_id, category_id, valuation_class_id);
