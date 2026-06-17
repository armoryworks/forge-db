CREATE INDEX ix_webhook_subscriptions_is_active ON public.webhook_subscriptions USING btree (is_active);
