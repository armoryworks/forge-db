DO $$
DECLARE
    fc integer;
BEGIN
    SELECT b.functional_currency_id INTO fc
    FROM public.acct_books b
    WHERE b.is_active
    ORDER BY b.id
    LIMIT 1;

    IF fc IS NULL THEN
        SELECT c.id INTO fc
        FROM public.currencies c
        WHERE c.is_base_currency
        ORDER BY c.id
        LIMIT 1;
    END IF;

    IF fc IS NULL THEN
        RETURN;
    END IF;

    UPDATE public.customers cu
    SET currency_id = c.id
    FROM public.currencies c
    WHERE cu.currency_id IS NULL
      AND cu.default_currency IS NOT NULL
      AND c.is_active
      AND upper(c.code) = upper(btrim(cu.default_currency));

    UPDATE public.quotes SET currency_id = fc WHERE currency_id IS NULL;

    UPDATE public.sales_orders SET currency_id = fc WHERE currency_id IS NULL;

    UPDATE public.purchase_orders po
    SET currency_id = c.id
    FROM public.currencies c
    WHERE po.currency_id IS NULL
      AND upper(c.code) = upper(btrim(po.quote_currency));

    UPDATE public.purchase_orders SET currency_id = fc WHERE currency_id IS NULL;

    UPDATE public.payments p
    SET currency_id = first_app.currency_id,
        fx_rate = CASE WHEN first_app.currency_id = fc THEN 1 ELSE first_app.settlement_fx_rate END
    FROM (
        SELECT DISTINCT ON (pa.payment_id) pa.payment_id, i.currency_id, pa.settlement_fx_rate
        FROM public.payment_applications pa
        JOIN public.invoices i ON i.id = pa.invoice_id
        ORDER BY pa.payment_id, pa.id
    ) first_app
    WHERE p.id = first_app.payment_id
      AND p.currency_id IS NULL;

    UPDATE public.payments SET currency_id = fc WHERE currency_id IS NULL;

    UPDATE public.vendor_payments vp
    SET currency_id = first_app.currency_id,
        fx_rate = CASE WHEN first_app.currency_id = fc THEN 1 ELSE first_app.settlement_fx_rate END
    FROM (
        SELECT DISTINCT ON (a.vendor_payment_id) a.vendor_payment_id, b.currency_id, a.settlement_fx_rate
        FROM public.vendor_payment_applications a
        JOIN public.vendor_bills b ON b.id = a.vendor_bill_id
        ORDER BY a.vendor_payment_id, a.id
    ) first_app
    WHERE vp.id = first_app.vendor_payment_id
      AND vp.currency_id IS NULL;

    UPDATE public.vendor_payments SET currency_id = fc WHERE currency_id IS NULL;

    IF fc <> 1 THEN
        UPDATE public.invoices SET currency_id = fc WHERE currency_id = 1 AND fx_rate = 1;
        UPDATE public.vendor_bills SET currency_id = fc WHERE currency_id = 1 AND fx_rate = 1;
    END IF;
END $$;
