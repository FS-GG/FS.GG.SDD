---
title: Versioning Policy
category: SDD
categoryindex: 6
index: 16
description: How the FS.GG.SDD.* packages and the fsgg-sdd CLI are versioned, and what each version bump promises about public-contract compatibility.
---

# Versioning Policy

This document is a **projection** of the policy of record at
`specs/018-release-readiness/contracts/versioning-policy.md` and of the machine
contract at [`release-readiness.json`](release-readiness.json). On any
disagreement, the contracts are authoritative. It is never a second source of
truth. (FR-001)

The policy basis is [Semantic Versioning](https://semver.org/).

## Single version source

All `FS.GG.SDD.*` packages and the `fsgg-sdd` CLI share **one** semantic version,
sourced from `Directory.Build.props` `<Version>` — currently **`0.2.0`**:

- `FS.GG.SDD.Artifacts`
- `FS.GG.SDD.Commands`
- `FS.GG.SDD.Cli` (the `fsgg-sdd` CLI)

The generator version (`currentGeneratorVersion`) is reconciled to the same
number, so `release-readiness.json` carries `identity.version = "0.2.0"` and
`generatorVersion.version = "0.2.0"`. A consumer can therefore determine the
release version deterministically from package metadata or
`release-readiness.json` without reading source. (FR-003)

The `channel` is derived from the version: a major of `0` is `preRelease`; a
major of `>=1` is `stable`. The current release is `preRelease`.

## Change class to bump rule

A **public contract** is any public schema, generated-view shape, command-output
(`--json`) contract, or CLI surface (command, flag, or exit code).

| Change | Class | Bump | Migration note |
|---|---|---|---|
| Remove/rename/retype a public field; change an output shape; remove/rename a command or flag; change an exit-code contract | **Breaking** | major | **required** |
| Add an optional field; add a generated-view kind; add a command/flag; add an optional report field | **Additive** | minor | none |
| Docs, internal refactor, comment, no public-contract change | **Clarifying** | patch | none |

The version delta between any two releases is explainable solely from this table
applied to the set of public-contract changes between them.

## Pre-1.0 (0.x) semantics

The current line is `0.x`. Under SemVer, a `0.y.z` line **MAY** introduce a
breaking change on a **minor** bump. This is stated explicitly so early adopters
are not surprised. A migration note is **still required** for any breaking
change, pre-1.0 included. The first `1.0.0` line freezes the surfaces currently
classed `Stable`.

## Schema-version vs contract-version divergence

A generated view's internal `schemaVersion` and a cross-repo `contractVersion`
(currently only `governance-handoff.json`, at `contractVersion` `1.0.0`) move
**independently**:

- A breaking change to **either** number ⇒ **major** package bump.
- An additive change to **either** number ⇒ **minor** package bump.

The [schema reference](schema-reference.md) records both numbers so each bump is
auditable.

## Stability vocabulary

Each public contract (and field, where useful) carries one classification:

- **Stable** — frozen; any shape change is Breaking and bumps major.
- **AdditiveOptional** — may gain optional fields under a minor bump; consumers
  MUST tolerate unknown fields.
- **Experimental** — may change under a minor bump with a note; not yet frozen.

## Migration-note obligation

- A release with **any** Breaking change MUST ship a migration note at
  `docs/release/migrations/<version>.md` enumerating each breaking
  public-contract change and the corresponding consumer adaptation step.
- An **additive-only** release MUST NOT require a migration note; its absence is
  consistent with this policy.

See [migrations/README.md](migrations/README.md) for the obligation in detail and
the current index of notes. (FR-009 / FR-010)

## Out of scope

This policy governs SDD-owned public contracts only. It does **not** govern, and
makes no claims about:

- Governance `fsgg release` / `fsgg verify` gate schemas and exit codes;
- release enforcement rules (publish plans, trusted publishing, provenance);
- public-registry account/signing setup;
- scheduled exhaustive CI validation matrices.

These are Governance- or release-ops-owned. (FR-014)
