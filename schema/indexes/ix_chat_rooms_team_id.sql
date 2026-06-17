CREATE INDEX ix_chat_rooms_team_id ON public.chat_rooms USING btree (team_id) WHERE (team_id IS NOT NULL);
