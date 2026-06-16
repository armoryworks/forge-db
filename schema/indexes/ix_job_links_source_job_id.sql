CREATE INDEX ix_job_links_source_job_id ON public.job_links USING btree (source_job_id);
