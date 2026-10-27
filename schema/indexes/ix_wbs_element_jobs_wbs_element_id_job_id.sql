CREATE UNIQUE INDEX ix_wbs_element_jobs_wbs_element_id_job_id ON public.wbs_element_jobs USING btree (wbs_element_id, job_id);
