CREATE UNIQUE INDEX ux_training_path_translations_path_locale ON public.training_path_translations USING btree (training_path_id, locale);
