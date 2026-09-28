# C3-SDD-01 — Ordinary V2 receiver adoption

Status: activation source prepared against published Coordination CLI `0.1.6` and enrolled
custody. Installed settlement and its idempotent rerun remain pending until this change merges.

The fixed `sdd-v1` profile names `FS-GG/FS.GG.SDD`, repository ID `1274272672`.
Disabled source landed in PR #1080 as `6aed9a6ac8bbfbe46062f92298a684f40933b94d`;
its settlement run `36445508395` was skipped. The receiver follows qualified Net commit
`94c2d0acb7addb416615110949afa984a5219b12` and successful native run `36441643056`.
The observer is unchanged. The qualifier binds SDD's identity and native producers; both exact
source digests remain in policy.

## Native gates and activation boundary

The selected settlement checks are `Deterministic gate (locked restore + build + test)` and
`Shared-build-config drift check`. The separate native gate population preserves those two plus
`API compatibility gate (breaking-change → SemVer major)`, `kit / coordination-kit`,
`skill-view-check`, and `materialize / receiver-validate`. All six use Actions App `15368`.
The first three come from gate workflow `303425212`; kit, skill-view, and materialization come
from workflows `307166760`, `321983347`, and `316861917`. Their paths and pull-request event
are fixed in both policy and code. Missing any native gate refuses qualification.

The protected-main push workflow runs a read-only, secret-free predecessor. Only its exact-run
receipt can admit the `ordinary-v2` credential job. That job rechecks the receipt and current
Authority, verifies the served archive against its exact SHA-256, installs only that archive
from a local-only feed, and invokes the installed CLI once. Both checkouts persist no credential.
There are no request or manual trigger paths. The shared Authority App, authorizer, repository,
writer/integrity rulesets, and `OpenV2` boundary remain unchanged.

## Custody and package evidence

Dedicated environment `22938103320` has sole `main` branch policy `61311037` and no reviewers.
The protected sealed bridge source is `3ed9b4f8316a14c5e15bfb4c1e11554730b9ae3f`, run
`36450825631`, attempt 1. Artifact `10983781659` has digest
`sha256:4f434a9f6355aeb820bdca954ffd4d0988cd3b0401d99720e7d807aa9441840b`.
After signed-packet verification and three encrypted PUTs, independent readback confirmed exactly
`V2_ORDINARY_APP_ID`, `V2_ORDINARY_APP_PRIVATE_KEY`, and
`V2_ORDINARY_AUTHORIZER_PRIVATE_KEY`. No secret value or ciphertext packet is committed.

[Coordination CLI v0.1.6](https://github.com/FS-GG/FS.GG.Coordination/releases/tag/v0.1.6)
binds source `275cccb30a5c9ade4b3bba344ede13d7df446d13` and archive SHA-256
`0f5d92799af84acb8663df0f524dc2ccfe54cfcc0bc6ad2183e867c8cdd47730`.
Publisher retry `36457989575` completed successfully after NuGet index propagation delayed the
first attempt. The tag is protected by active ruleset `21633398`, which forbids update and deletion
without bypass actors. GitHub's release API reports `immutable=false`; this is not a claim of
platform-enforced release-asset immutability. The receiver enforces the exact archive digest.

The successful protected publisher's GitHub Packages readback matches that archive exactly.
Independent anonymous downloads verified the public release archive and NuGet payload; NuGet's
signed archive SHA-256 is `a0f30dfb4aa9d435a7484964db7f0e269d68a2e7a3fb564612081a2c8727e5a2`.
Only the repository signature differs. Direct local org-feed access returned 403, so that feed's
provenance is the successful protected publisher and its public readback asset, not a claimed
local credentialed download.

A fresh anonymous public-only install of exactly `0.1.6` passed. The installed assembly's
`sdd-v1` profile has the exact repository ID, two settlement checks, and all six native gates;
an unknown selector refuses. The installed command also loaded and refused safely without
GitHub or credential context. That smoke is not a settlement result.

## Delivery and operating proof

Run both receiver Python fixtures, preserve all six native required gates, merge the exact green
activation head and read back protected main. Inspect the resulting protected-push preflight
receipt and settlement result. Independently read the exact Authority shard and operation,
then rerun the same workflow and require `AlreadyComplete` with an unchanged receipt, one effect,
and unchanged Authority ref. Pending or failed operation remains pending.

This repository-owned receiver does not change lifecycle APIs, package versions, fresh scaffolds,
generated workspaces, or scaffold defaults. It imports no V1 admission or receiver state. There
is no workspace upgrade or live efficiency claim.
