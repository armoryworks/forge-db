CREATE UNIQUE INDEX ix_event_attendees_event_id_user_id ON public.event_attendees USING btree (event_id, user_id);
