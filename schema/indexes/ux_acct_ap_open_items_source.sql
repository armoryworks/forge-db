CREATE UNIQUE INDEX ux_acct_ap_open_items_source ON public.acct_ap_open_items USING btree (source_type, source_id);
