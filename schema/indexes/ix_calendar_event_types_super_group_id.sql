CREATE INDEX ix_calendar_event_types_super_group_id ON public.calendar_event_types USING btree (super_group_id);
