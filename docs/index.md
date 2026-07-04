# FS.GG.SDD Docs

Start with:

- [Quickstart](quickstart.md)
- [Authoring Contracts](reference/authoring-contracts.md)
- [Artifact Taxonomy](reference/artifact-taxonomy.md)
- [Doctor & Upgrade](reference/doctor-upgrade.md)
- [Migration from Spec Kit](migration-from-spec-kit.md)
- [Adopting Governance](adopting-governance.md)
- [Initial design](initial-design.md)

Release and distribution:

- [Installation](release/installation.md)
- [Versioning Policy](release/versioning-policy.md)
- [Compatibility Matrix](release/compatibility-matrix.md)
- [Schema Reference](release/schema-reference.md)
- [Migration Notes](release/migrations/README.md)

More:

- [Initial implementation plan](initial-implementation-plan.md)
- [Decision 0001: separate SDD product](decisions/0001-separate-sdd-product.md)
- [Imported reference docs](reference/README.md)

This repository is the FS.GG.SDD lifecycle product: `fsgg-sdd` CLI source and tests
live under `src/` and `tests/`, and feature specs are authored under `specs/`. New
work is added through feature specs and plans, not by editing product source
directly.

The current feature plan is the SDD-owned roadmap and the primary project
direction document used by agent guidance.

The `reference/` directory contains copied development context from
`FS.GG.Governance` and the FS-GG org docs. Treat those files as provenance and
source material; SDD-owned decisions should be recorded in this repository's own
docs.
