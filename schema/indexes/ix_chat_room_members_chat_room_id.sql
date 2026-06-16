CREATE INDEX ix_chat_room_members_chat_room_id ON public.chat_room_members USING btree (chat_room_id);
