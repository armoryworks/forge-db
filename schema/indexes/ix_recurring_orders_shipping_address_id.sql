CREATE INDEX ix_recurring_orders_shipping_address_id ON public.recurring_orders USING btree (shipping_address_id);
