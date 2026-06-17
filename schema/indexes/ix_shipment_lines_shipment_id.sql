CREATE INDEX ix_shipment_lines_shipment_id ON public.shipment_lines USING btree (shipment_id);
