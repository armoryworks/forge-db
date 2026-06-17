CREATE UNIQUE INDEX ix_invoices_shipment_id ON public.invoices USING btree (shipment_id);
