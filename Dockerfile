# forge-db — the declarative-schema CLI, packaged so a deploy can reconcile a live
# database to the desired schema in one shot (`forge-db apply --db <url>`). The
# schema/ tree and the pg-schema-diff engine are baked in, so it runs with no host
# setup. Used by forge-deploy as a pre-update step (backup -> apply -> swap app).

# ── Stage 1: pg-schema-diff (the diff/apply engine) ────────────────────────────
FROM golang:1.26-bookworm AS pgsd
# Go 1.26 matches the forge-api schema-drift-check workflow; pg-schema-diff v1.0.5
# requires Go >= 1.25.5. MIT, no account.
RUN go install github.com/stripe/pg-schema-diff/cmd/pg-schema-diff@v1.0.5
# -> /go/bin/pg-schema-diff

# ── Stage 2: build the Forge.Db CLI ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Forge.Db/Forge.Db.csproj src/Forge.Db/
RUN dotnet restore src/Forge.Db/Forge.Db.csproj
COPY src/ src/
RUN dotnet publish src/Forge.Db/Forge.Db.csproj -c Release -o /app /p:UseAppHost=false

# ── Stage 3: runtime ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

# The diff engine on PATH. PgSchemaDiffRunner looks for it via PG_SCHEMA_DIFF_BIN
# (or "pg-schema-diff" on PATH).
COPY --from=pgsd /go/bin/pg-schema-diff /usr/local/bin/pg-schema-diff
ENV PG_SCHEMA_DIFF_BIN=/usr/local/bin/pg-schema-diff

# The CLI.
COPY --from=build /app /app

# The desired-state schema tree (plus premigrate/data/seed/history). The CLI
# resolves the repo root by walking up from the working directory for schema/, so
# WORKDIR is the baked-in root and callers only need `--db <url>`.
#
# premigrate/ is NOT optional here. A missing directory is a silent no-op in the
# runner (Discover yields nothing), so omitting it would ship an image that skips
# phase 0 and lets the reconcile plan DROP + CREATE over a rename — the exact
# data loss that phase exists to prevent, with no error to notice.
WORKDIR /forge-db
COPY schema/ /forge-db/schema/
COPY premigrate/ /forge-db/premigrate/
COPY data/ /forge-db/data/
COPY seed/ /forge-db/seed/
COPY scrub/ /forge-db/scrub/
COPY history/ /forge-db/history/

ENTRYPOINT ["dotnet", "/app/forge-db.dll"]
# Default to a no-op help so a bare `docker run` is harmless; callers pass e.g.
#   apply --db postgres://... --env prod --yes --backup-taken
CMD ["version"]
