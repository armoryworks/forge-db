CREATE INDEX ix_audit_log_entries_user_id ON public.audit_log_entries USING btree (user_id);
