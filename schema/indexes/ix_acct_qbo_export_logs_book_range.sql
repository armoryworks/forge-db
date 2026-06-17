CREATE INDEX ix_acct_qbo_export_logs_book_range ON public.acct_qbo_export_logs USING btree (book_id, from_date, to_date);
