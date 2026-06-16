CREATE INDEX ix_bin_movements_scan_action_log_id ON public.bin_movements USING btree (scan_action_log_id) WHERE (scan_action_log_id IS NOT NULL);
