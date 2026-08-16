CREATE FUNCTION public.communication_artifacts_immutability() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- An artifact is evidence: the raw message, or a document it carried, with
    -- the SHA-256 taken at ingestion before anything else read the bytes. If a
    -- row can be edited or removed, the hash attests to nothing — someone can
    -- always claim the record was changed after the fact, which is precisely
    -- the claim this table exists to defeat.
    --
    -- Unlike the ledger's equivalent there is no permitted-mutation carve-out
    -- and no sandbox exemption. There is no legitimate reason to alter a stored
    -- copy of something a customer sent. Retention purges operate on the
    -- communication and its storage object, not by rewriting artifact rows.
    IF (TG_OP = 'DELETE') THEN
        RAISE EXCEPTION
            'Communication artifact immutability violation: artifact % (sha256 %) cannot be deleted. It is the evidence a hash was taken over.',
            OLD.id, OLD.sha256
            USING ERRCODE = 'restrict_violation';
    END IF;

    RAISE EXCEPTION
        'Communication artifact immutability violation: artifact % (sha256 %) is write-once and cannot be modified.',
        OLD.id, OLD.sha256
        USING ERRCODE = 'restrict_violation';
END;
$$;
