CREATE UNIQUE INDEX ux_acct_inventory_valuations_book_part ON public.acct_inventory_valuations USING btree (book_id, part_id);
