CREATE INDEX ix_events_created_by_user_id ON public.events USING btree (created_by_user_id);
