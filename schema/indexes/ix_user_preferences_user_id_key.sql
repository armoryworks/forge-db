CREATE UNIQUE INDEX ix_user_preferences_user_id_key ON public.user_preferences USING btree (user_id, key);
