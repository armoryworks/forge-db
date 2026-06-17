CREATE INDEX ix_pick_lines_shipment_line_id ON public.pick_lines USING btree (shipment_line_id);
