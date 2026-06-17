CREATE INDEX ix_scheduled_tasks_is_active ON public.scheduled_tasks USING btree (is_active);
