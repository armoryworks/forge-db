CREATE UNIQUE INDEX ux_acct_qbo_account_maps_gl_account ON public.acct_qbo_account_maps USING btree (gl_account_id) WHERE (deleted_at IS NULL);
