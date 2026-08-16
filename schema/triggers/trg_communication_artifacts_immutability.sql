CREATE TRIGGER trg_communication_artifacts_immutability BEFORE DELETE OR UPDATE ON public.communication_artifacts FOR EACH ROW EXECUTE FUNCTION public.communication_artifacts_immutability();
