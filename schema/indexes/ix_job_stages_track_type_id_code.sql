CREATE UNIQUE INDEX ix_job_stages_track_type_id_code ON public.job_stages USING btree (track_type_id, code);
