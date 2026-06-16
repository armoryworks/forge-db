CREATE INDEX ix_announcements_department_id ON public.announcements USING btree (department_id) WHERE (department_id IS NOT NULL);
