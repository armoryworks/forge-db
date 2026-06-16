CREATE INDEX ix_chat_messages_sender_id ON public.chat_messages USING btree (sender_id);
