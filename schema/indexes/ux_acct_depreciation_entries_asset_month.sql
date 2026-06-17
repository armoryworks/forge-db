CREATE UNIQUE INDEX ux_acct_depreciation_entries_asset_month ON public.acct_depreciation_entries USING btree (fixed_asset_id, period_month);
