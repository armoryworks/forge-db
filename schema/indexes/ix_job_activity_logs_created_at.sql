CREATE INDEX ix_job_activity_logs_created_at ON public.job_activity_logs USING btree (created_at);
