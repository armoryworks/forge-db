CREATE INDEX ix_vendor_bill_lines_gl_account ON public.vendor_bill_lines USING btree (gl_account_id);
