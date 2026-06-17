CREATE TABLE public.announcement_teams (
    announcement_id integer NOT NULL,
    team_id integer NOT NULL
);

ALTER TABLE ONLY public.announcement_teams
    ADD CONSTRAINT pk_announcement_teams PRIMARY KEY (announcement_id, team_id);

ALTER TABLE ONLY public.announcement_teams
    ADD CONSTRAINT fk_announcement_teams__teams_team_id FOREIGN KEY (team_id) REFERENCES public.teams(id) ON DELETE CASCADE;

ALTER TABLE ONLY public.announcement_teams
    ADD CONSTRAINT fk_announcement_teams_announcements_announcement_id FOREIGN KEY (announcement_id) REFERENCES public.announcements(id) ON DELETE CASCADE;
