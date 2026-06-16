CREATE INDEX ix_file_attachments_uploaded_by_id ON public.file_attachments USING btree (uploaded_by_id);
