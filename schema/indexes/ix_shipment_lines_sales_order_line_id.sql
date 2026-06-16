CREATE INDEX ix_shipment_lines_sales_order_line_id ON public.shipment_lines USING btree (sales_order_line_id);
