CREATE INDEX ix_mfa_recovery_codes_user_id ON public.mfa_recovery_codes USING btree (user_id);
