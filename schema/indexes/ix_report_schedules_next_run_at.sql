CREATE INDEX ix_report_schedules_next_run_at ON public.report_schedules USING btree (next_run_at);
