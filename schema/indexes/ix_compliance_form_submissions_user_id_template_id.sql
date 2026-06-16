CREATE INDEX ix_compliance_form_submissions_user_id_template_id ON public.compliance_form_submissions USING btree (user_id, template_id);
