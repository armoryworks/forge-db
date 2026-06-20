-- forge-db apply plan (audit receipt — NOT replayable; schema/ + pg-schema-diff are the source of truth)
-- env: dev    captured: 20260620T071740Z

/*
Statement 0
*/
SET SESSION statement_timeout = 3000;
SET SESSION lock_timeout = 3000;
ALTER TABLE "public"."shipments" ADD COLUMN "height" numeric(12,4);

/*
Statement 1
*/
SET SESSION statement_timeout = 3000;
SET SESSION lock_timeout = 3000;
ALTER TABLE "public"."shipments" ADD COLUMN "length" numeric(12,4);

/*
Statement 2
*/
SET SESSION statement_timeout = 3000;
SET SESSION lock_timeout = 3000;
ALTER TABLE "public"."shipments" ADD COLUMN "width" numeric(12,4);

