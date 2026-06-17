CREATE INDEX ix_non_conformances_containment_by_id ON public.non_conformances USING btree (containment_by_id);
