CREATE INDEX ix_parts_procurement_source_inventory_class ON public.parts USING btree (procurement_source, inventory_class);
