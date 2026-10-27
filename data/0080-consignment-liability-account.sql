INSERT INTO public.acct_gl_accounts
    (book_id, account_number, name, account_type, normal_balance, is_control_account, is_postable, is_active)
SELECT b.id, '21100', 'Consignment Liability', 'Liability', 'Credit', false, true, true
FROM public.acct_books b
WHERE EXISTS (
        SELECT 1 FROM public.acct_account_determination_rules r
        WHERE r.book_id = b.id AND r.key = 'GRNI')
  AND NOT EXISTS (
        SELECT 1 FROM public.acct_account_determination_rules r
        WHERE r.book_id = b.id AND r.key = 'CONSIGNMENT_LIABILITY')
  AND NOT EXISTS (
        SELECT 1 FROM public.acct_gl_accounts a
        WHERE a.book_id = b.id AND a.account_number = '21100');

INSERT INTO public.acct_account_determination_rules (book_id, key, gl_account_id)
SELECT a.book_id, 'CONSIGNMENT_LIABILITY', a.id
FROM public.acct_gl_accounts a
WHERE a.account_number = '21100'
  AND a.name = 'Consignment Liability'
  AND NOT EXISTS (
        SELECT 1 FROM public.acct_account_determination_rules r
        WHERE r.book_id = a.book_id AND r.key = 'CONSIGNMENT_LIABILITY');
