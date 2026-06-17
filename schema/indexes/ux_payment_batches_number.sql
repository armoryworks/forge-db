CREATE UNIQUE INDEX ux_payment_batches_number ON public.payment_batches USING btree (batch_number);
