CREATE INDEX ix_webhook_deliveries_is_success ON public.webhook_deliveries USING btree (is_success);
