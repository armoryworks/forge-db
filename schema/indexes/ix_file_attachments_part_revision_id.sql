CREATE INDEX ix_file_attachments_part_revision_id ON public.file_attachments USING btree (part_revision_id);
