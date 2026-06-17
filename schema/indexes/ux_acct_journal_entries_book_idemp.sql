CREATE UNIQUE INDEX ux_acct_journal_entries_book_idemp ON public.acct_journal_entries USING btree (book_id, idempotency_key) WHERE (idempotency_key IS NOT NULL);
