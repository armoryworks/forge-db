CREATE INDEX ix_approval_steps_approver_user_id ON public.approval_steps USING btree (approver_user_id);
