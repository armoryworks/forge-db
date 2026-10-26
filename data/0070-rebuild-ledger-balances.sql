WITH j AS (
    SELECT l.book_id,
           l.gl_account_id,
           e.fiscal_period_id,
           l.currency_id,
           sum(CASE WHEN l.debit > 0 THEN l.functional_amount ELSE 0 END) AS debit_total,
           sum(CASE WHEN l.credit > 0 THEN l.functional_amount ELSE 0 END) AS credit_total
    FROM public.acct_journal_lines l
    JOIN public.acct_journal_entries e ON e.id = l.journal_entry_id
    WHERE e.status IN ('Posted', 'Reversed')
    GROUP BY l.book_id, l.gl_account_id, e.fiscal_period_id, l.currency_id)
UPDATE public.acct_ledger_balances b
SET debit_total = j.debit_total,
    credit_total = j.credit_total
FROM j
WHERE b.book_id = j.book_id
  AND b.gl_account_id = j.gl_account_id
  AND b.fiscal_period_id = j.fiscal_period_id
  AND b.currency_id = j.currency_id
  AND (b.debit_total <> j.debit_total OR b.credit_total <> j.credit_total);

WITH j AS (
    SELECT l.book_id,
           l.gl_account_id,
           e.fiscal_period_id,
           l.currency_id,
           sum(CASE WHEN l.debit > 0 THEN l.functional_amount ELSE 0 END) AS debit_total,
           sum(CASE WHEN l.credit > 0 THEN l.functional_amount ELSE 0 END) AS credit_total
    FROM public.acct_journal_lines l
    JOIN public.acct_journal_entries e ON e.id = l.journal_entry_id
    WHERE e.status IN ('Posted', 'Reversed')
    GROUP BY l.book_id, l.gl_account_id, e.fiscal_period_id, l.currency_id)
INSERT INTO public.acct_ledger_balances (book_id, gl_account_id, fiscal_period_id, currency_id, debit_total, credit_total)
SELECT j.book_id, j.gl_account_id, j.fiscal_period_id, j.currency_id, j.debit_total, j.credit_total
FROM j
WHERE NOT EXISTS (
    SELECT 1 FROM public.acct_ledger_balances b
    WHERE b.book_id = j.book_id
      AND b.gl_account_id = j.gl_account_id
      AND b.fiscal_period_id = j.fiscal_period_id
      AND b.currency_id = j.currency_id);

WITH j AS (
    SELECT l.book_id,
           l.gl_account_id,
           e.fiscal_period_id,
           l.currency_id,
           sum(CASE WHEN l.debit > 0 THEN l.functional_amount ELSE 0 END) AS debit_total,
           sum(CASE WHEN l.credit > 0 THEN l.functional_amount ELSE 0 END) AS credit_total
    FROM public.acct_journal_lines l
    JOIN public.acct_journal_entries e ON e.id = l.journal_entry_id
    WHERE e.status IN ('Posted', 'Reversed')
    GROUP BY l.book_id, l.gl_account_id, e.fiscal_period_id, l.currency_id)
DELETE FROM public.acct_ledger_balances b
WHERE NOT EXISTS (
    SELECT 1 FROM j
    WHERE b.book_id = j.book_id
      AND b.gl_account_id = j.gl_account_id
      AND b.fiscal_period_id = j.fiscal_period_id
      AND b.currency_id = j.currency_id);
