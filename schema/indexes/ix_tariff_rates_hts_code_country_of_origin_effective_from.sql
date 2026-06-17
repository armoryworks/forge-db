CREATE INDEX ix_tariff_rates_hts_code_country_of_origin_effective_from ON public.tariff_rates USING btree (hts_code, country_of_origin, effective_from);
