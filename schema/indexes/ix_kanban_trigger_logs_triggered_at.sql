CREATE INDEX ix_kanban_trigger_logs_triggered_at ON public.kanban_trigger_logs USING btree (triggered_at);
