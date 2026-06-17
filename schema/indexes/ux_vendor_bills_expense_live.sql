CREATE UNIQUE INDEX ux_vendor_bills_expense_live ON public.vendor_bills USING btree (expense_id) WHERE ((expense_id IS NOT NULL) AND (status <> 4));
