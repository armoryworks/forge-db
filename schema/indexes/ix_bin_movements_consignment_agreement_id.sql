CREATE INDEX ix_bin_movements_consignment_agreement_id ON public.bin_movements USING btree (consignment_agreement_id) WHERE (consignment_agreement_id IS NOT NULL);
