CREATE UNIQUE INDEX ux_training_module_translations_module_locale ON public.training_module_translations USING btree (training_module_id, locale);
