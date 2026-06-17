CREATE INDEX ix_activity_logs_created_at ON public.activity_logs USING btree (created_at);
