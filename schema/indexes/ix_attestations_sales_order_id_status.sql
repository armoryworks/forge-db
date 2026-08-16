CREATE INDEX ix_attestations_sales_order_id_status ON public.attestations USING btree (sales_order_id, status);
