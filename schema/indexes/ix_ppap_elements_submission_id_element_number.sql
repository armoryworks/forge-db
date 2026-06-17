CREATE UNIQUE INDEX ix_ppap_elements_submission_id_element_number ON public.ppap_elements USING btree (submission_id, element_number);
