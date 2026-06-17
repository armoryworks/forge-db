CREATE INDEX ix_tax_documents_user_id_tax_year ON public.tax_documents USING btree (user_id, tax_year);
