CREATE UNIQUE INDEX ux_acct_ar_open_items_source ON public.acct_ar_open_items USING btree (source_type, source_id);
