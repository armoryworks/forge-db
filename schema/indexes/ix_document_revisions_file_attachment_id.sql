CREATE INDEX ix_document_revisions_file_attachment_id ON public.document_revisions USING btree (file_attachment_id);
