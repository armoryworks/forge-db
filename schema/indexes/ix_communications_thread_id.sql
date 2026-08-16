CREATE INDEX ix_communications_thread_id ON public.communications USING btree (thread_id) WHERE (thread_id IS NOT NULL);
