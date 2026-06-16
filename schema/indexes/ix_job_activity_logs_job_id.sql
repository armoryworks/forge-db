CREATE INDEX ix_job_activity_logs_job_id ON public.job_activity_logs USING btree (job_id);
