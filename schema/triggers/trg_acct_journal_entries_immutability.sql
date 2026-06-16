CREATE TRIGGER trg_acct_journal_entries_immutability BEFORE DELETE OR UPDATE ON public.acct_journal_entries FOR EACH ROW EXECUTE FUNCTION public.acct_journal_entries_immutability();
