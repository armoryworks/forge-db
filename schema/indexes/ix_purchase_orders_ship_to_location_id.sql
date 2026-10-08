CREATE INDEX ix_purchase_orders_ship_to_location_id ON public.purchase_orders USING btree (ship_to_location_id);
