CREATE UNIQUE INDEX ix_icp_rubrics_is_default ON public.icp_rubrics USING btree (is_default) WHERE ((is_default = true) AND (deleted_at IS NULL));
