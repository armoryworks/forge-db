WITH terms (id, plural_id) AS (
    VALUES
        ('job', 'jobs'),
        ('customer', 'customers'),
        ('vendor', 'vendors'),
        ('part', 'parts'),
        ('work_center', 'work_centers'),
        ('quote', 'quotes'),
        ('sales_order', 'sales_orders'),
        ('purchase_order', 'purchase_orders'),
        ('invoice', 'invoices'),
        ('shipment', 'shipments'),
        ('lead', 'leads'),
        ('employee', 'employees'),
        ('asset', 'assets'),
        ('planning_cycle', 'planning_cycles')
),
bad_terms AS (
    SELECT t.id, t.plural_id
    FROM terms t
    WHERE EXISTS (
        SELECT 1
        FROM public.terminology_entries e
        WHERE e.key IN ('entity_' || t.id || ':es', 'label_' || t.plural_id || ':es')
          AND e.deleted_at IS NULL
          AND e.source_preset_id = 'machine-translation'
          AND e.label <> ''
          AND (char_length(e.label) > 60 OR e.label ~ '[.!?;:\r\n]')
    )
)
UPDATE public.terminology_entries e
SET label = '',
    is_admin_edited = true,
    source_preset_id = 'machine-translation',
    updated_at = now()
FROM bad_terms b
WHERE e.key IN ('entity_' || b.id || ':es', 'label_' || b.plural_id || ':es', 'gender_' || b.id || ':es')
  AND e.deleted_at IS NULL
  AND e.label <> ''
  AND NOT (e.is_admin_edited AND e.source_preset_id IS DISTINCT FROM 'machine-translation');
