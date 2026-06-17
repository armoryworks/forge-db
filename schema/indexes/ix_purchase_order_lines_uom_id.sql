CREATE INDEX ix_purchase_order_lines_uom_id ON public.purchase_order_lines USING btree (uom_id);
