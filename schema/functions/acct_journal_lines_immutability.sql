CREATE FUNCTION public.acct_journal_lines_immutability() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    owner_status varchar(20);
    target_entry_id bigint;
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

    -- The owning header is the same for OLD and NEW (re-parenting a line is
    -- itself a forbidden mutation, caught below), so OLD's FK is authoritative.
    target_entry_id := OLD.journal_entry_id;

    SELECT status INTO owner_status
    FROM acct_journal_entries
    WHERE id = target_entry_id;

    IF (owner_status IN ('Posted', 'Reversed')) THEN
        RAISE EXCEPTION
            'Ledger immutability violation: journal line % on a % entry cannot be %. Corrections are made via reversing entries only.',
            OLD.id, owner_status, lower(TG_OP)
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF (TG_OP = 'DELETE') THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;
