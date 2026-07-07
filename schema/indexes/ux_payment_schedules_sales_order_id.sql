CREATE UNIQUE INDEX ux_payment_schedules_sales_order_id ON public.payment_schedules USING btree (sales_order_id) WHERE ((sales_order_id IS NOT NULL) AND (deleted_at IS NULL));
