CREATE INDEX ix_scheduled_tasks_next_run_at ON public.scheduled_tasks USING btree (next_run_at);
