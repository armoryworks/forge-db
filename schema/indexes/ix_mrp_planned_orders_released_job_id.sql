CREATE INDEX ix_mrp_planned_orders_released_job_id ON public.mrp_planned_orders USING btree (released_job_id);
