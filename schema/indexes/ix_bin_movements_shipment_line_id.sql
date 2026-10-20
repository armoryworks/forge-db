CREATE INDEX ix_bin_movements_shipment_line_id ON public.bin_movements USING btree (shipment_line_id) WHERE (shipment_line_id IS NOT NULL);
