CREATE INDEX ix_spc_control_limits_characteristic_id_is_active ON public.spc_control_limits USING btree (characteristic_id, is_active);
