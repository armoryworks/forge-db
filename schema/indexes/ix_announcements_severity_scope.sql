CREATE INDEX ix_announcements_severity_scope ON public.announcements USING btree (severity, scope) WHERE (deleted_at IS NULL);
