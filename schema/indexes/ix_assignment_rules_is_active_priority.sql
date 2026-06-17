CREATE INDEX ix_assignment_rules_is_active_priority ON public.assignment_rules USING btree (is_active, priority);
