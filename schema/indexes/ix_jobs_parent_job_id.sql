CREATE INDEX ix_jobs_parent_job_id ON public.jobs USING btree (parent_job_id);
