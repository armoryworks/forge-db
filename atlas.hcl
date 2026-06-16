# Atlas project config for forge-db (docs/DESIGN.md §4).
#
# The Forge.Db harness normally drives Atlas via explicit flags (--url / --to / --dev-url /
# --exclude), so it does NOT depend on this file. atlas.hcl exists so you can also run the `atlas`
# CLI directly and so the project's conventions live in one declared place.
#
# The dev URL must be a pgvector-capable Postgres (the schema declares `CREATE EXTENSION vector`);
# a plain postgres:* dev DB will fail to load the desired state.

variable "url" {
  type    = string
  default = getenv("FORGE_DB_URL")
}

variable "dev_url" {
  type    = string
  default = getenv("FORGE_DB_DEV_URL")
}

env "forge" {
  url = var.url
  dev = var.dev_url
  src = "file://schema"

  # public only; EF Core's migration-history table is owned by EF, not forge-db.
  schemas = ["public"]
  exclude = ["__EFMigrationsHistory", "__EFMigrationsHistory_pre_squash"]
}
