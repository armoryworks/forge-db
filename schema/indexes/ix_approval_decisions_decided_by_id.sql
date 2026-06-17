CREATE INDEX ix_approval_decisions_decided_by_id ON public.approval_decisions USING btree (decided_by_id);
