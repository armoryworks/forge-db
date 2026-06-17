CREATE INDEX ix_machine_data_points_tag_id_timestamp ON public.machine_data_points USING btree (tag_id, "timestamp");
