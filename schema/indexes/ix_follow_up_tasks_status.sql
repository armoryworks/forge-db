CREATE INDEX ix_follow_up_tasks_status ON public.follow_up_tasks USING btree (status) WHERE ((status)::text = 'Open'::text);
