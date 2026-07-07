CREATE UNIQUE INDEX ux_customer_po_documents_sales_order_id ON public.customer_po_documents USING btree (sales_order_id) WHERE (deleted_at IS NULL);
