CREATE TABLE public.workflow_run_entities (
    run_id integer NOT NULL,
    entity_type character varying(64) NOT NULL,
    entity_id integer NOT NULL,
    role character varying(32) NOT NULL
);

ALTER TABLE ONLY public.workflow_run_entities
    ADD CONSTRAINT pk_workflow_run_entities PRIMARY KEY (run_id, entity_type, entity_id);

ALTER TABLE ONLY public.workflow_run_entities
    ADD CONSTRAINT fk_workflow_run_entities_workflow_runs_run_id FOREIGN KEY (run_id) REFERENCES public.workflow_runs(id) ON DELETE CASCADE;


--
--
