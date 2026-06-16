CREATE INDEX ix_downtime_logs_job_id ON public.downtime_logs USING btree (job_id);
