CREATE INDEX ix_job_links_target_job_id ON public.job_links USING btree (target_job_id);
