CREATE INDEX ix_maintenance_predictions_asset_id_rule_key ON public.maintenance_predictions USING btree (asset_id, rule_key);
