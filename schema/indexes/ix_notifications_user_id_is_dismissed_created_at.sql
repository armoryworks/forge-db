CREATE INDEX ix_notifications_user_id_is_dismissed_created_at ON public.notifications USING btree (user_id, is_dismissed, created_at);
