CREATE INDEX ix_scheduled_operations_schedule_run_id ON public.scheduled_operations USING btree (schedule_run_id);
