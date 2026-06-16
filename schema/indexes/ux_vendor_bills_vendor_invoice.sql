CREATE UNIQUE INDEX ux_vendor_bills_vendor_invoice ON public.vendor_bills USING btree (vendor_id, vendor_invoice_number) WHERE (vendor_invoice_number IS NOT NULL);
