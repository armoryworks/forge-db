CREATE INDEX ix_time_entries_open_timer_start ON public.time_entries USING btree (timer_start) WHERE ((timer_start IS NOT NULL) AND (timer_stop IS NULL) AND (deleted_at IS NULL));
