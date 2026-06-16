CREATE UNIQUE INDEX ix_work_center_calendars_work_center_id_date ON public.work_center_calendars USING btree (work_center_id, date);
