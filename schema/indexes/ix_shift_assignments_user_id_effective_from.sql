CREATE INDEX ix_shift_assignments_user_id_effective_from ON public.shift_assignments USING btree (user_id, effective_from);
