CREATE INDEX ix_customer_refunds_payment_id ON public.customer_refunds USING btree (payment_id);
