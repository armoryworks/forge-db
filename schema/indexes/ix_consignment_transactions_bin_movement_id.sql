CREATE INDEX ix_consignment_transactions_bin_movement_id ON public.consignment_transactions USING btree (bin_movement_id) WHERE (bin_movement_id IS NOT NULL);
