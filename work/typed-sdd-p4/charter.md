---
schemaVersion: 1
workId: typed-sdd-p4
title: Typed SDD additive lifecycle backend
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# Typed SDD additive lifecycle backend Charter

## Identity
- Work id: `typed-sdd-p4`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Keep one lifecycle stage model; representation backends change authority and authoring, not stage meaning.
- Publish the FS.GG.SDD producer boundary before any provider or workspace consumes `typed-sdd`.
- Preserve explicit `none` and `sdd`, keep omitted lifecycle selection at `sdd`, and do not extend `spec-kit`.
- Treat the compiled `SpecificationModel` as Typed SDD authority; Markdown is a review projection only.
- Refuse ambiguity and unsupported semantics rather than guessing during Standard SDD migration.
- Require installed packages, tools, and generated skills; no S.I.R. checkout or source-project reference.

## Scope Boundaries
- Own the lifecycle choice, representation backend, compiler, author/inspect/migrate operations, provenance,
  refresh/upgrade/doctor/readiness/ship checks, installed skills, fixtures, and producer documentation.
- Defer provider descriptors, raw product templates, workspace wizard, registry rollout, and consumer pins to
  downstream repositories after these artifacts are published.
- Keep SDD lifecycle ownership separate from optional Governance enforcement; the deferred Typed SDD
  constitution/Governance integration design does not enter this milestone.
- Do not flip the omitted default. P5 owns that separate versioned decision after P4 compatibility evidence.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work typed-sdd-p4`.
