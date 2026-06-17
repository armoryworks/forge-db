CREATE INDEX ix_workflow_runs_last_activity_at ON public.workflow_runs USING btree (last_activity_at);
