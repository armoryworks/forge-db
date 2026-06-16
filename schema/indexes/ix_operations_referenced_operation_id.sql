CREATE INDEX ix_operations_referenced_operation_id ON public.operations USING btree (referenced_operation_id);
