CREATE INDEX ix_chat_messages_recipient_id ON public.chat_messages USING btree (recipient_id);
