CREATE INDEX ix_acct_gl_accounts_parent ON public.acct_gl_accounts USING btree (parent_account_id);
