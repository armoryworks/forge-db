CREATE UNIQUE INDEX ux_customer_refunds_refund_number ON public.customer_refunds USING btree (refund_number);
