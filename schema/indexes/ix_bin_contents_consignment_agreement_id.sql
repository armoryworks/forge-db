CREATE INDEX ix_bin_contents_consignment_agreement_id ON public.bin_contents USING btree (consignment_agreement_id) WHERE (consignment_agreement_id IS NOT NULL);
