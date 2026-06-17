CREATE INDEX ix_identity_documents_verified_by_id ON public.identity_documents USING btree (verified_by_id);
