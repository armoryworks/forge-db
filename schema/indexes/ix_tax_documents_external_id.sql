CREATE UNIQUE INDEX ix_tax_documents_external_id ON public.tax_documents USING btree (external_id) WHERE (external_id IS NOT NULL);
