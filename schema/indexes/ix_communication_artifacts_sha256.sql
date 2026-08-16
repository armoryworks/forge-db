CREATE INDEX ix_communication_artifacts_sha256 ON public.communication_artifacts USING btree (sha256);
