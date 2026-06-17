CREATE UNIQUE INDEX ix_training_progress_user_id_module_id ON public.training_progress USING btree (user_id, module_id);
