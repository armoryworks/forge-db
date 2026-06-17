CREATE INDEX ix_mrp_demands_parent_planned_order_id ON public.mrp_demands USING btree (parent_planned_order_id);
