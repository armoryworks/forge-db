CREATE INDEX ix_vendor_scorecards_vendor_id_period_start ON public.vendor_scorecards USING btree (vendor_id, period_start);
