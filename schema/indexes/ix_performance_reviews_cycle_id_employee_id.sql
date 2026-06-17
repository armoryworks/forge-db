CREATE UNIQUE INDEX ix_performance_reviews_cycle_id_employee_id ON public.performance_reviews USING btree (cycle_id, employee_id);
