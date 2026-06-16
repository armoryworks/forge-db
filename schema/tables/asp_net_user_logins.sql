CREATE TABLE public.asp_net_user_logins (
    login_provider text NOT NULL,
    provider_key text NOT NULL,
    provider_display_name text,
    user_id integer NOT NULL
);

ALTER TABLE ONLY public.asp_net_user_logins
    ADD CONSTRAINT pk_asp_net_user_logins PRIMARY KEY (login_provider, provider_key);

ALTER TABLE ONLY public.asp_net_user_logins
    ADD CONSTRAINT fk_asp_net_user_logins__asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES public.asp_net_users(id) ON DELETE CASCADE;
