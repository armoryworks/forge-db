CREATE INDEX ix_credit_holds_placed_by_id ON public.credit_holds USING btree (placed_by_id);
