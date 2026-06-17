CREATE INDEX ix_part_prices_part_id_effective_to ON public.part_prices USING btree (part_id, effective_to);
