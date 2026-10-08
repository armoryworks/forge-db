CREATE INDEX ix_work_centers_team_id ON public.work_centers USING btree (team_id) WHERE (team_id IS NOT NULL);
