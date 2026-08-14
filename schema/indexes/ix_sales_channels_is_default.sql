CREATE UNIQUE INDEX ix_sales_channels_is_default ON public.sales_channels USING btree (is_default) WHERE (is_default = true);
