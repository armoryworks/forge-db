CREATE UNIQUE INDEX ux_payment_run_items_payment_run_id_vendor_bill_id ON public.payment_run_items USING btree (payment_run_id, vendor_bill_id);
