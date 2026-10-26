CREATE UNIQUE INDEX ux_payment_terms_legacy_credit_terms ON public.payment_terms USING btree (legacy_credit_terms);
