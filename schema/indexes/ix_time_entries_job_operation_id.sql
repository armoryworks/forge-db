CREATE INDEX ix_time_entries_job_operation_id ON public.time_entries USING btree (job_operation_id) WHERE (job_operation_id IS NOT NULL);
