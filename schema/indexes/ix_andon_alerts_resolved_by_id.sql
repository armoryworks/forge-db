CREATE INDEX ix_andon_alerts_resolved_by_id ON public.andon_alerts USING btree (resolved_by_id);
