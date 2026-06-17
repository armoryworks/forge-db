CREATE INDEX ix_recurring_orders_next_generation_date ON public.recurring_orders USING btree (next_generation_date);
