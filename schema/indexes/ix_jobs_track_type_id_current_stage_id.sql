CREATE INDEX ix_jobs_track_type_id_current_stage_id ON public.jobs USING btree (track_type_id, current_stage_id);
