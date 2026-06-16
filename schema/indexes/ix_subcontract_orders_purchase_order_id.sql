CREATE INDEX ix_subcontract_orders_purchase_order_id ON public.subcontract_orders USING btree (purchase_order_id);
