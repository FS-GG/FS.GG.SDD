# C3-SDD-01 — Ordinary V2 receiver adoption

Status: local activation preparation only. Disabled source is protected in PR #1080;
dedicated custody is enrolled. Package selection, activation, and installed settlement
remain pending. This draft must not be pushed or admitted before public release verification.

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

1. Disabled source landed as `6aed9a6ac8bbfbe46062f92298a684f40933b94d` through all six
   native gates and receiver fixtures. Settlement run `36445508395` was skipped.
2. Dedicated custody was enrolled from protected bridge commit
   `3ed9b4f8316a14c5e15bfb4c1e11554730b9ae3f`, run `36450825631`, attempt 1.
   Artifact `10983781659` has digest
   `sha256:4f434a9f6355aeb820bdca954ffd4d0988cd3b0401d99720e7d807aa9441840b`.
   The signed ciphertext packet was verified before three encrypted PUTs; independent
   readback confirms exactly the three names and the same sole main branch policy.
   The policy retains the public evidence; no packet or secret value is committed.
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

## Package proof and local activation join

Coordination `0.1.6` source is protected at
`275cccb30a5c9ade4b3bba344ede13d7df446d13`; preparation run `36450952246` is not publication
evidence. The local workflow therefore keeps its hard-disabled preflight, `UNSELECTED` version
and digest, and `credentialJob.installed = false`. The staged credential job already binds
the exact predecessor receipt, rechecks current public Authority, verifies the archive before
local-only installation, and invokes the installed CLI once. It cannot run in this draft.

Before replacing those placeholders:

1. Read the immutable public `v0.1.6` release and exact tag/source commit. Download its
   `FS.GG.Coordination.Cli.0.1.6.nupkg` anonymously, hash the served bytes, and verify the
   release manifest, package identity, and source provenance. Retain producer publication
   evidence and applicable feed-coherence evidence; a local build is insufficient.
2. In a fresh disposable directory, copy only that verified archive into a local feed.
   Install with a NuGet configuration containing `<clear/>` and only that feed, a fresh
   task-specific package cache, explicit `--version 0.1.6`, and `--no-cache`. Verify the
   installed tool/dependency payload against the archive and inspect the packaged provider's
   fixed `sdd-v1` repository identity, two settlement checks, and six native gate checks.
   Run a credential-free command refusal as a smoke test; it proves loading, not settlement.
3. Refresh repository identity, all check/workflow producers, environment restriction and
   exact three secret names, and shared Authority. Replace both workflow placeholders and
   policy nulls with the verified version/digest/source, mark the package verified, update
   policy installed state and remove the preflight disable together. Update the focused
   assertions to bind that exact release. Run both Python fixtures and inspect the diff.
4. Admit one managed activation PR, preserve all six native required gates, merge its exact
   green head and read back protected main. Require the native settlement result and an
   `AlreadyComplete` rerun before recording installed operation. A failed or pending run
   stays pending; it is not repaired by copying an earlier receiver's receipt.
