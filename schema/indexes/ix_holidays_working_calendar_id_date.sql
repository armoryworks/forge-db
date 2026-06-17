CREATE INDEX ix_holidays_working_calendar_id_date ON public.holidays USING btree (working_calendar_id, date);
