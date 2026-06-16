CREATE UNIQUE INDEX ix_work_center_shifts_work_center_id_shift_id ON public.work_center_shifts USING btree (work_center_id, shift_id);
