# C3-SDD-01 — Ordinary V2 receiver adoption

Status: disabled receiver source. Dedicated environment prepared; credential enrollment,
published package selection, activation, and installed settlement remain pending.

This repository uses the fixed `sdd-v1` source profile for `FS-GG/FS.GG.SDD`
(repository ID `1274272672`). The receiver follows qualified Net commit
`94c2d0acb7addb416615110949afa984a5219b12` and its successful settlement run
`36441643056`. The observer is unchanged; the qualifier binds SDD's native identity and
check producers. Their exact digests are recorded in the receiver policy.

## Source and acceptance

The two selected settlement checks are `Deterministic gate (locked restore + build + test)`
and `Shared-build-config drift check`. The separate native gate population preserves those
two plus `API compatibility gate (breaking-change → SemVer major)`,
`kit / coordination-kit`, `skill-view-check`, and `materialize / receiver-validate`.
All six use Actions App `15368`. The first three come from gate workflow `303425212`;
kit, skill-view, and materialization come from workflows `307166760`, `321983347`,
and `316861917`. The policy and code bind their exact paths and pull-request event.

The main-push workflow is hard-disabled, contains no credential job or package installer,
and accepts no request or manual trigger. The secret-free fixtures reject wrong repository,
profile, check producer, missing gates, failed checks, and foreign/stale evidence. A separate
read-only source-test workflow runs those fixtures without touching the native gate population.
The shared Authority anchor retains its existing writer App, authorizer, repository and rulesets.

The protected-main readback on 2026-09-28 was
`9865a78514e7d60280d20f8aab3275018c0e8b81`. The dedicated `ordinary-v2` environment is
ID `22938103320`, with sole `main` branch policy `61311037`, no reviewers, and zero secrets
at source preparation. Enrollment must use the protected sealed custody bridge and independently
read back exactly `V2_ORDINARY_APP_ID`, `V2_ORDINARY_APP_PRIVATE_KEY`, and
`V2_ORDINARY_AUTHORIZER_PRIVATE_KEY`. No secret values belong in source or evidence logs.

## Ordered remaining window

1. Land this disabled source through all six native required gates and receiver fixtures;
   verify the protected merge and the skipped settlement job.
2. Enroll and read back dedicated custody. Source delivery and an empty environment do not
   establish credentials or settlement authority.
3. Independently verify the shared Coordination CLI `0.1.6` publication supports `sdd-v1`.
   Bind its immutable served archive, digest, and source commit. Version and digest remain
   unselected until that evidence exists.
4. In a separate activation change, refresh SDD identity, all six native gates and producer
   mappings, custody, and current Authority. Enable the secret-free receipt predecessor and
   receipt-fenced installed CLI settlement together. Prove native settlement and an
   `AlreadyComplete` rerun before recording installed operation.

This repository-owned receiver does not change SDD lifecycle APIs, package versions, fresh
scaffolds, generated workspaces, or scaffold defaults. It imports no V1 admission or receiver
state and leaves `OpenV2` unchanged. There is no workspace upgrade or live efficiency claim.
