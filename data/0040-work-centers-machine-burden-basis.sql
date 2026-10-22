UPDATE public.work_centers wc
SET burden_basis = 1,
    updated_at = now()
WHERE wc.burden_basis = 0
  AND wc.deleted_at IS NULL
  AND EXISTS (
      SELECT 1
      FROM public.assets a
      WHERE a.deleted_at IS NULL
        AND a.asset_type = 0
        AND (a.id = wc.asset_id OR a.work_center_id = wc.id)
  );
