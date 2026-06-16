CREATE INDEX ix_audit_log_entries_action ON public.audit_log_entries USING btree (action);
