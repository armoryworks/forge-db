CREATE INDEX ix_follow_up_tasks_assigned_to_user_id ON public.follow_up_tasks USING btree (assigned_to_user_id);
