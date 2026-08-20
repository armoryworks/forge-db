CREATE UNIQUE INDEX ix_business_identifiers_value_active ON public.business_identifiers USING btree (value) WHERE is_active;
