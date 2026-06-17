CREATE INDEX ix_cycle_counts_location_id_counted_at ON public.cycle_counts USING btree (location_id, counted_at);
