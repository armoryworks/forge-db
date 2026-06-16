CREATE INDEX ix_vendor_bill_lines_po_line ON public.vendor_bill_lines USING btree (purchase_order_line_id);
