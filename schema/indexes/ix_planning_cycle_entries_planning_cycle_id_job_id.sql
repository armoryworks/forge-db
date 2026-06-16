CREATE UNIQUE INDEX ix_planning_cycle_entries_planning_cycle_id_job_id ON public.planning_cycle_entries USING btree (planning_cycle_id, job_id);
