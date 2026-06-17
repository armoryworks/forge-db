CREATE INDEX ix_maintenance_schedules_next_due_at ON public.maintenance_schedules USING btree (next_due_at);
