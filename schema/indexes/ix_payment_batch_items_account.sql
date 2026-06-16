CREATE INDEX ix_payment_batch_items_account ON public.payment_batch_items USING btree (vendor_bank_account_id);
