CREATE INDEX ix_customer_returns_restock_location_id ON public.customer_returns USING btree (restock_location_id);
