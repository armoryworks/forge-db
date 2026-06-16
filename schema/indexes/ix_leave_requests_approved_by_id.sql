CREATE INDEX ix_leave_requests_approved_by_id ON public.leave_requests USING btree (approved_by_id);
