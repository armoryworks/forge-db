CREATE UNIQUE INDEX ix_quotes_quote_number ON public.quotes USING btree (quote_number) WHERE (quote_number IS NOT NULL);
