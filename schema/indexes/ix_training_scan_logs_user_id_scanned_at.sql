CREATE INDEX ix_training_scan_logs_user_id_scanned_at ON public.training_scan_logs USING btree (user_id, scanned_at);
