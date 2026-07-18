CREATE UNIQUE INDEX ux_parts_gtin ON public.parts USING btree (gtin) WHERE (gtin IS NOT NULL AND deleted_at IS NULL);
