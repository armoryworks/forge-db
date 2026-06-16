CREATE INDEX ix_sync_queue_entries_status_created_at ON public.sync_queue_entries USING btree (status, created_at);
