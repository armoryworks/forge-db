CREATE INDEX ix_reservations_sales_order_line_id ON public.reservations USING btree (sales_order_line_id);
