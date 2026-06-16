CREATE INDEX ix_auto_po_suggestions_converted_purchase_order_id ON public.auto_po_suggestions USING btree (converted_purchase_order_id) WHERE (converted_purchase_order_id IS NOT NULL);
