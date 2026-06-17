CREATE INDEX ix_workflow_runs_started_by_user_id ON public.workflow_runs USING btree (started_by_user_id);
