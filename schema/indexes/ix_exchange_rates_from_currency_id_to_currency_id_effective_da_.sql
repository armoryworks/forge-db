CREATE UNIQUE INDEX "ix_exchange_rates_from_currency_id_to_currency_id_effective_da~" ON public.exchange_rates USING btree (from_currency_id, to_currency_id, effective_date);
