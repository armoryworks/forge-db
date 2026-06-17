CREATE INDEX ix_document_revisions_authored_by_id ON public.document_revisions USING btree (authored_by_id);
