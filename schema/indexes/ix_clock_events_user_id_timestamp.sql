CREATE INDEX ix_clock_events_user_id_timestamp ON public.clock_events USING btree (user_id, "timestamp");
