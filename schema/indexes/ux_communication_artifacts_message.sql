CREATE UNIQUE INDEX ux_communication_artifacts_message ON public.communication_artifacts USING btree (communication_id) WHERE (kind = 'Message');
