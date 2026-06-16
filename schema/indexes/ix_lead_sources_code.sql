CREATE UNIQUE INDEX ix_lead_sources_code ON public.lead_sources USING btree (code) WHERE (deleted_at IS NULL);
