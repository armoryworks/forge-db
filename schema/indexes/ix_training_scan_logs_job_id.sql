CREATE INDEX ix_training_scan_logs_job_id ON public.training_scan_logs USING btree (job_id) WHERE (job_id IS NOT NULL);
