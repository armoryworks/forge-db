CREATE INDEX ix_consignment_transactions_vendor_bill_id ON public.consignment_transactions USING btree (vendor_bill_id) WHERE (vendor_bill_id IS NOT NULL);
