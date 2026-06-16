CREATE INDEX ix_audit_log_entries_created_at ON public.audit_log_entries USING btree (created_at);
