CREATE UNIQUE INDEX ix_price_list_entries_price_list_id_part_id_min_quantity ON public.price_list_entries USING btree (price_list_id, part_id, min_quantity);
