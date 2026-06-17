CREATE INDEX ix_holidays_working_calendar_id_observed_date ON public.holidays USING btree (working_calendar_id, observed_date);
