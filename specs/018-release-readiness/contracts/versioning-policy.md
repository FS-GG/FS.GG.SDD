# Contract: Versioning Policy (policy of record)

Defines how the `FS.GG.SDD.*` packages and the `fsgg-sdd` CLI are versioned
(FR-001). The published `docs/release/versioning-policy.md` is a projection of
this contract. Basis: Semantic Versioning (spec Assumption "Versioning scheme",
constitution Change Classification).

## Single version source

All packages (`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, `FS.GG.SDD.Cli`) and
the CLI share **one** semantic version sourced from `Directory.Build.props`
`<Version>`. The generator version (`currentGeneratorVersion`) is reconciled to
the same number. A consumer determines the release version deterministically from
package metadata or `release-readiness.json` without reading source (FR-003).

## Change-class → bump rule (FR-001)

A "public contract" is any public schema, generated-view shape, command-output
(`--json`) contract, or CLI surface (command/flag/exit-code).

| Change | Class | Bump | Migration note |
|---|---|---|---|
| Remove/rename/retype a public field; change an output shape; remove/rename a command or flag; change an exit-code contract | **Breaking** | major | **required** |
| Add an optional field; add a generated-view kind; add a command/flag; add an optional report field | **Additive** | minor | none |
| Docs, internal refactor, comment, no public-contract change | **Clarifying** | patch | none |

### Pre-1.0 semantics (current `0.x` line)
Under SemVer a `0.y.z` line MAY introduce a breaking change on a **minor** bump.
This is stated so early adopters are not surprised (spec edge case). A migration
note is **still required** for any breaking change, pre-1.0 included. The first
`1.0.0` line freezes the surfaces currently classed `stable`.

### Schema-version vs contract-version divergence
A generated view's internal `schemaVersion` and a cross-repo `contractVersion`
(e.g. `governance-handoff.json`) move independently. A breaking change to
**either** ⇒ major package bump; an additive change to **either** ⇒ minor. The
schema reference records both numbers so the bump is auditable (edge case).

## Migration-note obligation (FR-009 / FR-010 / SC-006)

- A release with **any** Breaking change MUST ship a migration note at
  `docs/release/migrations/<version>.md` enumerating each breaking public-contract
  change and the corresponding consumer adaptation step.
- An **additive-only** release MUST NOT require a migration note; its absence is
  consistent with this policy (acceptance scenario US4-3).

## Stability classification (FR-004)

Each public contract (and field, where useful) carries one of:

- **Stable** — frozen; any shape change is Breaking → major.
- **AdditiveOptional** — may gain optional fields under a minor bump; consumers
  MUST tolerate unknown fields.
- **Experimental** — may change under a minor bump with a note; not yet frozen.

## Out of scope (FR-014, spec Assumptions)

Governance `fsgg release` / `fsgg verify` gate schemas and exit codes, release
enforcement rules (publish plans, trusted publishing, provenance), public-registry
account/signing setup, and scheduled exhaustive CI matrices are **not** governed
by this policy — they are Governance- or release-ops-owned.
