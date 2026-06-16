CREATE TRIGGER trg_acct_journal_lines_immutability BEFORE DELETE OR UPDATE ON public.acct_journal_lines FOR EACH ROW EXECUTE FUNCTION public.acct_journal_lines_immutability();
