CREATE INDEX ix_job_subtasks_job_id ON public.job_subtasks USING btree (job_id);
