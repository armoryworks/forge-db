CREATE INDEX ix_chat_messages_file_attachment_id ON public.chat_messages USING btree (file_attachment_id);
