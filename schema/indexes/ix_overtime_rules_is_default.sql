CREATE UNIQUE INDEX ix_overtime_rules_is_default ON public.overtime_rules USING btree (is_default) WHERE ((is_default = true) AND (deleted_at IS NULL));
