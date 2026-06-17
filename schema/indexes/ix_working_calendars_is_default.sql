CREATE UNIQUE INDEX ix_working_calendars_is_default ON public.working_calendars USING btree (is_default) WHERE (is_default = true);
