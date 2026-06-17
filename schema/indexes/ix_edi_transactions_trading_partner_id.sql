CREATE INDEX ix_edi_transactions_trading_partner_id ON public.edi_transactions USING btree (trading_partner_id);
