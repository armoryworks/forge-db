CREATE TABLE public.asp_net_user_roles (
    user_id integer NOT NULL,
    role_id integer NOT NULL
);

ALTER TABLE ONLY public.asp_net_user_roles
    ADD CONSTRAINT pk_asp_net_user_roles PRIMARY KEY (user_id, role_id);

ALTER TABLE ONLY public.asp_net_user_roles
    ADD CONSTRAINT fk_asp_net_user_roles__asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES public.asp_net_users(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.asp_net_user_roles
    ADD CONSTRAINT fk_asp_net_user_roles_asp_net_roles_role_id FOREIGN KEY (role_id) REFERENCES public.asp_net_roles(id) ON DELETE CASCADE;
