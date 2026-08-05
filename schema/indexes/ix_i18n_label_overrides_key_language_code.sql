CREATE UNIQUE INDEX ix_i18n_label_overrides_key_language_code ON public.i18n_label_overrides USING btree (key, language_code) WHERE (deleted_at IS NULL);
