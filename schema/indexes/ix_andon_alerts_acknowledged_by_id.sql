CREATE INDEX ix_andon_alerts_acknowledged_by_id ON public.andon_alerts USING btree (acknowledged_by_id);
