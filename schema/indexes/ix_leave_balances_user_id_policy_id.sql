CREATE UNIQUE INDEX ix_leave_balances_user_id_policy_id ON public.leave_balances USING btree (user_id, policy_id);
