CREATE INDEX ix_approval_requests_requested_by_id ON public.approval_requests USING btree (requested_by_id);
