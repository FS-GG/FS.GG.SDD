---
name: typed-sdd-migrate
description: Analyze and explicitly accept Standard SDD to Typed SDD migrations.
---

# Typed SDD migrate

First run `fsgg-sdd typed-sdd migrate --work <id> --source work/<id>/spec.md --backend
quint-specification-v1 --cache <cache-root> --agent <agent-id> --session <session-id>` without `--accept`.
Review the `Migrated`, `Ambiguous`, or `Unsupported` classification, semantic diff, and rollback
digest, including the exact canonical v1 semantic-payload digest. Only a `Migrated` result may be
repeated with `--accept`; preaccept runs must write no bytes. The executable Quint catalogue remains
the closed Q1 requirements/evidence slice. The complete legacy model is retained losslessly and bound
by the literate Markdown, compiled-contract digest, and rollback inventory; do not describe arbitrary
legacy fields as newly executable Quint. An accepted migration authenticates every original v1 path
and byte under `.fsgg/typed-sdd-rollback/v1/<id>/`. Run
`fsgg-sdd typed-sdd rollback --work <id> --accept` to verify that inventory, restore the exact v1
tree, and remove v2 outputs. Corrupt or incomplete rollback material leaves the live tree unchanged.
