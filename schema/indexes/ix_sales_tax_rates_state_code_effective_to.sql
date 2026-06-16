CREATE INDEX ix_sales_tax_rates_state_code_effective_to ON public.sales_tax_rates USING btree (state_code, effective_to);
