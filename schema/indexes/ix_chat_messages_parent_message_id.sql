CREATE INDEX ix_chat_messages_parent_message_id ON public.chat_messages USING btree (parent_message_id) WHERE (parent_message_id IS NOT NULL);
