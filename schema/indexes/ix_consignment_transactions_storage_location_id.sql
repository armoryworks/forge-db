CREATE INDEX ix_consignment_transactions_storage_location_id ON public.consignment_transactions USING btree (storage_location_id) WHERE (storage_location_id IS NOT NULL);
