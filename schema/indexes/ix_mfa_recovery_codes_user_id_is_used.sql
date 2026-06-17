CREATE INDEX ix_mfa_recovery_codes_user_id_is_used ON public.mfa_recovery_codes USING btree (user_id, is_used);
