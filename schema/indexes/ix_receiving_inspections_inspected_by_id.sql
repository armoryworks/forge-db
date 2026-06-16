CREATE INDEX ix_receiving_inspections_inspected_by_id ON public.receiving_inspections USING btree (inspected_by_id);
