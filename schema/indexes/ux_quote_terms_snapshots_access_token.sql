CREATE UNIQUE INDEX ux_quote_terms_snapshots_access_token ON public.quote_terms_snapshots USING btree (access_token);
