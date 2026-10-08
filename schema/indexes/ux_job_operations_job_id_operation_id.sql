CREATE UNIQUE INDEX ux_job_operations_job_id_operation_id ON public.job_operations USING btree (job_id, operation_id) WHERE (deleted_at IS NULL);
