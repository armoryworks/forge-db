CREATE INDEX ix_downtime_logs_started_at ON public.downtime_logs USING btree (started_at);
