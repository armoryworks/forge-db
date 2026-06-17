CREATE UNIQUE INDEX ux_acct_journal_templates_book_name ON public.acct_journal_templates USING btree (book_id, name);
