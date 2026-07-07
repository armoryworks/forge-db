CREATE UNIQUE INDEX ux_payment_schedules_quote_id ON public.payment_schedules USING btree (quote_id) WHERE ((quote_id IS NOT NULL) AND (deleted_at IS NULL));
