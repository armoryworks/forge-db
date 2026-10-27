WITH posted AS (
    SELECT e.source_id AS issue_id,
           sum(l.debit) AS amount
    FROM public.acct_journal_entries e
    JOIN public.acct_journal_lines l ON l.journal_entry_id = e.id
    WHERE e.source_type = 'MaterialIssue'
      AND e.idempotency_key = 'Inventory:MaterialIssue:' || e.source_id
      AND e.status = 'Posted'
      AND l.job_id IS NOT NULL
      AND l.debit > 0
    GROUP BY e.source_id)
UPDATE public.material_issues mi
SET unit_cost = round(p.amount / mi.quantity, 4),
    updated_at = now()
FROM posted p
WHERE mi.id = p.issue_id
  AND mi.issue_type = 0
  AND mi.quantity > 0
  AND mi.unit_cost = 0
  AND mi.deleted_at IS NULL;
