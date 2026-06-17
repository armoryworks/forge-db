CREATE UNIQUE INDEX ix_training_path_enrollments_user_id_path_id ON public.training_path_enrollments USING btree (user_id, path_id);
