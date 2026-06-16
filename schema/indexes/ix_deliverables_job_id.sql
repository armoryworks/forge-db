CREATE INDEX ix_deliverables_job_id ON public.deliverables USING btree (job_id) WHERE (deleted_at IS NULL);
