UPDATE public.qc_inspection_results r
SET specification = i.specification,
    is_required = i.is_required
FROM public.qc_checklist_items i
WHERE r.checklist_item_id = i.id
  AND r.specification IS NULL
  AND r.is_required
  AND (i.specification IS NOT NULL OR NOT i.is_required);

UPDATE public.qc_inspection_results
SET is_required = false
WHERE checklist_item_id IS NULL
  AND specification IS NULL
  AND is_required;
