CREATE UNIQUE INDEX ix_company_locations_is_default ON public.company_locations USING btree (is_default) WHERE (is_default = true);
