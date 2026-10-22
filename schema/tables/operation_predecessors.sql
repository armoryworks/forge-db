CREATE TABLE public.operation_predecessors (
    operation_id integer NOT NULL,
    predecessor_operation_id integer NOT NULL,
    gate smallint DEFAULT 0 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

ALTER TABLE ONLY public.operation_predecessors
    ADD CONSTRAINT pk_operation_predecessors PRIMARY KEY (operation_id, predecessor_operation_id);

ALTER TABLE ONLY public.operation_predecessors
    ADD CONSTRAINT ck_operation_predecessors_gate CHECK (gate = ANY (ARRAY[0, 1]));

ALTER TABLE ONLY public.operation_predecessors
    ADD CONSTRAINT ck_operation_predecessors_not_self CHECK (operation_id <> predecessor_operation_id);

ALTER TABLE ONLY public.operation_predecessors
    ADD CONSTRAINT fk_operation_predecessors_operations_operation_id FOREIGN KEY (operation_id) REFERENCES public.operations(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.operation_predecessors
    ADD CONSTRAINT fk_operation_predecessors_operations_predecessor_operation_id FOREIGN KEY (predecessor_operation_id) REFERENCES public.operations(id) ON DELETE CASCADE;
