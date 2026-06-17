CREATE INDEX ix_inter_plant_transfers_to_plant_id ON public.inter_plant_transfers USING btree (to_plant_id);
