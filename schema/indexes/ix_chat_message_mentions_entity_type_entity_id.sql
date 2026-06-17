CREATE INDEX ix_chat_message_mentions_entity_type_entity_id ON public.chat_message_mentions USING btree (entity_type, entity_id);
