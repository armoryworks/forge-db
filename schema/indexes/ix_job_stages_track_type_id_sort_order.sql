CREATE INDEX ix_job_stages_track_type_id_sort_order ON public.job_stages USING btree (track_type_id, sort_order);
