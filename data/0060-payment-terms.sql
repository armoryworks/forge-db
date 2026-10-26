INSERT INTO public.payment_terms
    (code, name, net_days, discount_percent, discount_days, legacy_credit_terms, is_active, sort_order, is_system, created_at, updated_at)
VALUES
    ('DueOnReceipt', 'Due on receipt', 0, 0, 0, 0, true, 10, true, now(), now()),
    ('COD', 'COD', 0, 0, 0, NULL, true, 20, true, now(), now()),
    ('Net15', 'Net 15', 15, 0, 0, 1, true, 30, true, now(), now()),
    ('Net30', 'Net 30', 30, 0, 0, 2, true, 40, true, now(), now()),
    ('Net45', 'Net 45', 45, 0, 0, 3, true, 50, true, now(), now()),
    ('Net60', 'Net 60', 60, 0, 0, 4, true, 60, true, now(), now()),
    ('Net90', 'Net 90', 90, 0, 0, 5, true, 70, true, now(), now()),
    ('1/10Net30', '1/10 Net 30', 30, 1, 10, NULL, true, 80, true, now(), now()),
    ('2/10Net30', '2/10 Net 30', 30, 2, 10, NULL, true, 90, true, now(), now()),
    ('2/10Net60', '2/10 Net 60', 60, 2, 10, NULL, true, 100, true, now(), now())
ON CONFLICT (code) DO NOTHING;

UPDATE public.customers c
SET payment_terms_id = t.id
FROM public.payment_terms t
WHERE c.payment_terms_id IS NULL
  AND c.default_credit_terms IS NOT NULL
  AND t.legacy_credit_terms = c.default_credit_terms;

UPDATE public.sales_orders s
SET payment_terms_id = t.id
FROM public.payment_terms t
WHERE s.payment_terms_id IS NULL
  AND s.credit_terms IS NOT NULL
  AND t.legacy_credit_terms = s.credit_terms;

UPDATE public.invoices i
SET payment_terms_id = t.id
FROM public.payment_terms t
WHERE i.payment_terms_id IS NULL
  AND i.credit_terms IS NOT NULL
  AND t.legacy_credit_terms = i.credit_terms;

UPDATE public.vendor_bills b
SET payment_terms_id = t.id
FROM public.payment_terms t
WHERE b.payment_terms_id IS NULL
  AND b.credit_terms IS NOT NULL
  AND t.legacy_credit_terms = b.credit_terms;

UPDATE public.vendors v
SET payment_terms_id = (
    SELECT t.id
    FROM public.payment_terms t
    WHERE t.deleted_at IS NULL
      AND lower(regexp_replace(btrim(v.payment_terms), '[^a-zA-Z0-9]', '', 'g'))
          IN (lower(regexp_replace(t.code, '[^a-zA-Z0-9]', '', 'g')), lower(regexp_replace(t.name, '[^a-zA-Z0-9]', '', 'g')))
    ORDER BY t.sort_order, t.id
    LIMIT 1)
WHERE v.payment_terms_id IS NULL
  AND v.payment_terms IS NOT NULL
  AND btrim(v.payment_terms) <> '';
