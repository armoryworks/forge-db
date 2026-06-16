CREATE INDEX ix_deliverables_project_id ON public.deliverables USING btree (project_id) WHERE (deleted_at IS NULL);
