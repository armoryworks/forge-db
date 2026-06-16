CREATE UNIQUE INDEX ix_wbs_elements_project_id_code ON public.wbs_elements USING btree (project_id, code);
