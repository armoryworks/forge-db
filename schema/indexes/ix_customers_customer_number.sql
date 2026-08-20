CREATE UNIQUE INDEX ix_customers_customer_number ON public.customers USING btree (customer_number) WHERE customer_number IS NOT NULL;
