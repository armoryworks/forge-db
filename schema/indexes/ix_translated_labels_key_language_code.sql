CREATE UNIQUE INDEX ix_translated_labels_key_language_code ON public.translated_labels USING btree (key, language_code);
