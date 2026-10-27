CREATE INDEX ix_consignment_transactions_job_id ON public.consignment_transactions USING btree (job_id) WHERE (job_id IS NOT NULL);
