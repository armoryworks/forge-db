CREATE UNIQUE INDEX ix_job_parts_job_id_part_id ON public.job_parts USING btree (job_id, part_id);
