#!/usr/bin/env bash
# apply-schema.sh — turn-key "reconcile the live DB to schema/" wrapper.
#
# The Forge.Db harness (plan/verify/apply + DeployGates) does the real work; this
# script just removes the setup friction: it resolves the DB URL from the
# forge-deploy compose conventions, checks the diff engine is present, shows the
# plan, and then applies with the harness's safety gates intact.
#
# Usage (from anywhere):
#   apply-schema.sh                      # dev target: plan -> apply -> verify
#   apply-schema.sh --db <postgres-url>  # explicit target
#   apply-schema.sh --env prod --yes --backup-taken [--allow-destructive]
#   apply-schema.sh --plan-only          # show the plan, change nothing
#
# DB URL resolution order:
#   1. --db <url>
#   2. $FORGE_DB_URL
#   3. forge-deploy/.env (POSTGRES_USER/POSTGRES_PASSWORD/POSTGRES_DB/POSTGRES_PORT)
#      falling back to the compose defaults postgres/postgres/forge@localhost:5432
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

DB_URL="${FORGE_DB_URL:-}"
TARGET_ENV="dev"
PLAN_ONLY=0
PASSTHRU=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --db)        DB_URL="$2"; shift 2 ;;
    --env)       TARGET_ENV="$2"; shift 2 ;;
    --plan-only) PLAN_ONLY=1; shift ;;
    --yes|--backup-taken|--allow-destructive) PASSTHRU+=("$1"); shift ;;
    -h|--help)   grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "[apply-schema] unknown flag: $1 (see --help)" >&2; exit 64 ;;
  esac
done

# --- resolve DB URL from forge-deploy conventions when not given -------------
if [[ -z "${DB_URL}" ]]; then
  DEPLOY_ENV_FILE="${REPO_ROOT}/../forge-deploy/.env"
  PG_USER="postgres"; PG_PASS="postgres"; PG_DB="forge"; PG_PORT="5432"
  if [[ -f "${DEPLOY_ENV_FILE}" ]]; then
    # shellcheck disable=SC1090  # only POSTGRES_* style assignments are expected
    PG_USER="$(grep -E '^POSTGRES_USER=' "${DEPLOY_ENV_FILE}" | tail -1 | cut -d= -f2- || true)"
    PG_PASS="$(grep -E '^POSTGRES_PASSWORD=' "${DEPLOY_ENV_FILE}" | tail -1 | cut -d= -f2- || true)"
    PG_DB="$(grep -E '^POSTGRES_DB=' "${DEPLOY_ENV_FILE}" | tail -1 | cut -d= -f2- || true)"
    PG_PORT="$(grep -E '^POSTGRES_PORT=' "${DEPLOY_ENV_FILE}" | tail -1 | cut -d= -f2- || true)"
    PG_USER="${PG_USER:-postgres}"; PG_PASS="${PG_PASS:-postgres}"
    PG_DB="${PG_DB:-forge}"; PG_PORT="${PG_PORT:-5432}"
    echo "[apply-schema] DB settings from ${DEPLOY_ENV_FILE}"
  else
    echo "[apply-schema] no forge-deploy/.env found — using compose defaults"
  fi
  DB_URL="postgres://${PG_USER}:${PG_PASS}@localhost:${PG_PORT}/${PG_DB}?sslmode=disable"
fi
echo "[apply-schema] target: ${DB_URL%%:*}://…@${DB_URL#*@}  (env: ${TARGET_ENV})"

# --- diff engine present? ----------------------------------------------------
ENGINE="${PG_SCHEMA_DIFF_BIN:-pg-schema-diff}"
if ! command -v "${ENGINE}" >/dev/null 2>&1; then
  cat >&2 <<'EOF'
[apply-schema] pg-schema-diff not found. Install it (MIT, no account) with:
    go install github.com/stripe/pg-schema-diff/cmd/pg-schema-diff@v1.0.5
  then ensure $(go env GOPATH)/bin is on PATH, or set PG_SCHEMA_DIFF_BIN.
EOF
  exit 127
fi

run() { dotnet run --project "${REPO_ROOT}/src/Forge.Db" -- "$@"; }

# --- plan (always) -----------------------------------------------------------
echo "[apply-schema] plan:"
run plan --db "${DB_URL}" --repo "${REPO_ROOT}"
if [[ "${PLAN_ONLY}" -eq 1 ]]; then
  echo "[apply-schema] --plan-only: stopping before apply."
  exit 0
fi

# --- apply (harness DeployGates enforce dev/prod + destructive safety) -------
run apply --db "${DB_URL}" --repo "${REPO_ROOT}" --env "${TARGET_ENV}" ${PASSTHRU[@]+"${PASSTHRU[@]}"}

# --- verify round-trip -------------------------------------------------------
run verify --db "${DB_URL}" --repo "${REPO_ROOT}"
echo "[apply-schema] done — database matches schema/."
