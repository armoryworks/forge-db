CREATE INDEX ix_training_path_modules_path_id_position ON public.training_path_modules USING btree (path_id, "position");
