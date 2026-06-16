CREATE INDEX ix_labor_rates_user_id_effective_from ON public.labor_rates USING btree (user_id, effective_from);
