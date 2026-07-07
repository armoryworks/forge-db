CREATE FUNCTION public.acct_journal_entries_immutability() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- §5A.4 training sandbox (decision D2, 2026-07-07): the TRAINING book is resettable.
    -- DELETEs are exempt from immutability for that one book (matched by code, so no real
    -- book can ever qualify) — reset = wipe + reseed. UPDATEs are NOT exempt: sandbox
    -- entries stay append-only while in use, which is itself the lesson.
    IF (TG_OP = 'DELETE') AND EXISTS (
        SELECT 1 FROM public.acct_books b WHERE b.id = OLD.book_id AND b.code = 'TRAINING'
    ) THEN
        RETURN OLD;
    END IF;

    IF (TG_OP = 'DELETE') THEN
        IF OLD.status IN ('Posted', 'Reversed') THEN
            RAISE EXCEPTION
                'Ledger immutability violation: % journal entry % cannot be deleted. Corrections are made via reversing entries only.',
                OLD.status, OLD.id
                USING ERRCODE = 'restrict_violation';
        END IF;
        RETURN OLD;
    END IF;

    -- TG_OP = 'UPDATE'
    IF (OLD.status = 'Posted') THEN
        -- The ONLY permitted mutation is the Posted->Reversed status flip plus
        -- (optionally) populating reversed_by_entry_id. Anything else is rejected.
        IF (NEW.status IS DISTINCT FROM 'Reversed')
           OR (NEW.id                       IS DISTINCT FROM OLD.id)
           OR (NEW.book_id                  IS DISTINCT FROM OLD.book_id)
           OR (NEW.entry_number             IS DISTINCT FROM OLD.entry_number)
           OR (NEW.entry_date               IS DISTINCT FROM OLD.entry_date)
           OR (NEW.fiscal_period_id         IS DISTINCT FROM OLD.fiscal_period_id)
           OR (NEW.fiscal_year_id           IS DISTINCT FROM OLD.fiscal_year_id)
           OR (NEW.source                   IS DISTINCT FROM OLD.source)
           OR (NEW.source_type              IS DISTINCT FROM OLD.source_type)
           OR (NEW.source_id                IS DISTINCT FROM OLD.source_id)
           OR (NEW.idempotency_key          IS DISTINCT FROM OLD.idempotency_key)
           OR (NEW.currency_id              IS DISTINCT FROM OLD.currency_id)
           OR (NEW.memo                     IS DISTINCT FROM OLD.memo)
           OR (NEW.auto_reverse_next_period IS DISTINCT FROM OLD.auto_reverse_next_period)
           OR (NEW.reversal_of_entry_id     IS DISTINCT FROM OLD.reversal_of_entry_id)
           OR (NEW.approved_by              IS DISTINCT FROM OLD.approved_by)
           OR (NEW.posted_by                IS DISTINCT FROM OLD.posted_by)
           OR (NEW.posted_at                IS DISTINCT FROM OLD.posted_at)
        THEN
            RAISE EXCEPTION
                'Ledger immutability violation: posted journal entry % is append-only. The only permitted mutation is the Posted->Reversed flip + reversed_by_entry_id link.',
                OLD.id
                USING ERRCODE = 'restrict_violation';
        END IF;
        RETURN NEW;
    ELSIF (OLD.status = 'Reversed') THEN
        -- A reversed header is fully locked — no further mutation of any kind.
        RAISE EXCEPTION
            'Ledger immutability violation: reversed journal entry % is locked and cannot be modified.',
            OLD.id
            USING ERRCODE = 'restrict_violation';
    END IF;

    -- Draft / PendingApproval / Approved headers remain freely mutable.
    RETURN NEW;
END;
$$;
