CREATE INDEX ix_shipments_sales_order_id ON public.shipments USING btree (sales_order_id);
