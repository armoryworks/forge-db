CREATE INDEX ix_domain_event_failures_failed_at ON public.domain_event_failures USING btree (failed_at);
