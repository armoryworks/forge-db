CREATE UNIQUE INDEX ux_leave_balance_adjustments_opening ON public.leave_balance_adjustments USING btree (user_id, policy_id) WHERE ((adjustment_type = 0) AND (deleted_at IS NULL));
