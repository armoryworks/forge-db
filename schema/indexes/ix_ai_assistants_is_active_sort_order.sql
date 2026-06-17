CREATE INDEX ix_ai_assistants_is_active_sort_order ON public.ai_assistants USING btree (is_active, sort_order);
