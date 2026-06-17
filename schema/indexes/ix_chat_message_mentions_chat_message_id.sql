CREATE INDEX ix_chat_message_mentions_chat_message_id ON public.chat_message_mentions USING btree (chat_message_id);
