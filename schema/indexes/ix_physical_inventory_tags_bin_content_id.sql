CREATE INDEX ix_physical_inventory_tags_bin_content_id ON public.physical_inventory_tags USING btree (bin_content_id) WHERE (bin_content_id IS NOT NULL);
