CREATE TABLE public.asp_net_user_tokens (
    user_id integer NOT NULL,
    login_provider text NOT NULL,
    name text NOT NULL,
    value text
);

ALTER TABLE ONLY public.asp_net_user_tokens
    ADD CONSTRAINT pk_asp_net_user_tokens PRIMARY KEY (user_id, login_provider, name);

ALTER TABLE ONLY public.asp_net_user_tokens
    ADD CONSTRAINT fk_asp_net_user_tokens__asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES public.asp_net_users(id) ON DELETE CASCADE;
