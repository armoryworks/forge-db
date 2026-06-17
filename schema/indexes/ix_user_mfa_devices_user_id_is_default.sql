CREATE UNIQUE INDEX ix_user_mfa_devices_user_id_is_default ON public.user_mfa_devices USING btree (user_id, is_default) WHERE ((is_default = true) AND (deleted_at IS NULL));
