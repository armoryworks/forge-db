CREATE INDEX ix_purchase_order_lines_purchase_unit_id ON public.purchase_order_lines USING btree (purchase_unit_id);
