CREATE TABLE public.work_center_qualifications (
    user_id integer NOT NULL,
    work_center_id integer NOT NULL,
    qualified_at timestamp with time zone NOT NULL,
    qualified_by_id integer,
    notes character varying(500)
);

ALTER TABLE ONLY public.work_center_qualifications
    ADD CONSTRAINT pk_work_center_qualifications PRIMARY KEY (user_id, work_center_id);

ALTER TABLE ONLY public.work_center_qualifications
    ADD CONSTRAINT fk_work_center_qualifications__asp_net_users_qualified_by_id FOREIGN KEY (qualified_by_id) REFERENCES public.asp_net_users(id) ON DELETE SET NULL;

ALTER TABLE ONLY public.work_center_qualifications
    ADD CONSTRAINT fk_work_center_qualifications__asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES public.asp_net_users(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.work_center_qualifications
    ADD CONSTRAINT fk_work_center_qualifications_work_centers_work_center_id FOREIGN KEY (work_center_id) REFERENCES public.work_centers(id) ON DELETE CASCADE;
