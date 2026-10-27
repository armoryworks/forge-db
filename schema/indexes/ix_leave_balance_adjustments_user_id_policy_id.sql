CREATE INDEX ix_leave_balance_adjustments_user_id_policy_id ON public.leave_balance_adjustments USING btree (user_id, policy_id);
