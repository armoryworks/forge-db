CREATE INDEX ix_exchange_rates_to_currency_id ON public.exchange_rates USING btree (to_currency_id);
