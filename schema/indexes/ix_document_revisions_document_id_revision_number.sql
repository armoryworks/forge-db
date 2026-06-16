CREATE UNIQUE INDEX ix_document_revisions_document_id_revision_number ON public.document_revisions USING btree (document_id, revision_number);
