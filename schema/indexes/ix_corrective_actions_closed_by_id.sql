CREATE INDEX ix_corrective_actions_closed_by_id ON public.corrective_actions USING btree (closed_by_id);
