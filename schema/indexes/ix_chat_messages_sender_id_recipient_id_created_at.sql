CREATE INDEX ix_chat_messages_sender_id_recipient_id_created_at ON public.chat_messages USING btree (sender_id, recipient_id, created_at);
