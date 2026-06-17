CREATE INDEX ix_purchase_order_lines_part_id ON public.purchase_order_lines USING btree (part_id);
