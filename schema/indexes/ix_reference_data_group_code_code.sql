CREATE UNIQUE INDEX ix_reference_data_group_code_code ON public.reference_data USING btree (group_code, code);
