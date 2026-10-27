CREATE INDEX ix_consignment_transactions_journal_entry_id ON public.consignment_transactions USING btree (journal_entry_id) WHERE (journal_entry_id IS NOT NULL);
