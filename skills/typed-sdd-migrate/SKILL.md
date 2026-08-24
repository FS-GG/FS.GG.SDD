---
name: typed-sdd-migrate
description: Analyze and explicitly accept Standard SDD to Typed SDD migrations.
---

# Typed SDD migrate

First run `fsgg-sdd typed-sdd migrate --work <id> --source work/<id>/spec.md` without `--accept`.
Review the `Migrated`, `Ambiguous`, or `Unsupported` classification, semantic diff, and rollback
digest. Only a `Migrated` result may be repeated with `--accept`; preaccept runs must write no bytes.
An accepted migration preserves `work/<id>/spec.standard-sdd.rollback.md`.
