CREATE INDEX ix_forecast_overrides_demand_forecast_id ON public.forecast_overrides USING btree (demand_forecast_id);
