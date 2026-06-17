CREATE INDEX ix_scheduled_operations_job_id ON public.scheduled_operations USING btree (job_id);
