CREATE UNIQUE INDEX ix_employee_profiles_user_id ON public.employee_profiles USING btree (user_id) WHERE (user_id IS NOT NULL);
