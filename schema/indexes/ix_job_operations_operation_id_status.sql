CREATE INDEX ix_job_operations_operation_id_status ON public.job_operations USING btree (operation_id, status);
