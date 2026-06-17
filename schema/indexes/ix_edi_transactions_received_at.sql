CREATE INDEX ix_edi_transactions_received_at ON public.edi_transactions USING btree (received_at);
