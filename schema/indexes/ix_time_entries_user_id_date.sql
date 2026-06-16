CREATE INDEX ix_time_entries_user_id_date ON public.time_entries USING btree (user_id, date);
