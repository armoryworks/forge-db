CREATE UNIQUE INDEX ix_rfq_vendor_responses_rfq_id_vendor_id ON public.rfq_vendor_responses USING btree (rfq_id, vendor_id);
