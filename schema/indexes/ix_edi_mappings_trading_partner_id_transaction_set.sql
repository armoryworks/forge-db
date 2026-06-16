CREATE INDEX ix_edi_mappings_trading_partner_id_transaction_set ON public.edi_mappings USING btree (trading_partner_id, transaction_set);
