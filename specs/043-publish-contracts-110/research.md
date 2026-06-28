# Phase 0 Research: Publish FS.GG.Contracts 1.1.0 + durable coherence

No `NEEDS CLARIFICATION` markers remained after the spec. The open questions were all
mechanism/scope choices; each is resolved below.

## Decision 1 — Publish trigger: `workflow_dispatch` with `version=1.1.0`

**Decision**: Publish `1.1.0` by invoking the existing `release.yml` via
`workflow_dispatch` with an explicit `version=1.1.0` input. Do **not** edit the workflow and do
**not** mint a `v1.1.0` git tag or GitHub release.

**Rationale**: `release.yml`'s version-resolution state machine (feature 039) treats a
`workflow_dispatch` with a non-empty `version` input as a manual override: it publishes exactly
that version with `push=true`. The fsproj `<Version>` is already `1.1.0`, so the packed version
matches and the drift guard is satisfied. Crucially, the SDD **product** line is `0.2.0` while
the **contracts** line is `1.1.0`; a `release:`/`push: tags v*` trigger would mint a `v1.1.0`
tag that misrepresents the product version. The explicit-input dispatch publishes the contracts
package without polluting the product tag space.

**Alternatives considered**:
- *Cut a `v1.1.0` release/tag* — works (drift guard passes, fsproj is the authority) but mints a
  misleading product tag (`0.2.0` product, `v1.1.0` tag). Rejected.
- *No-input dispatch (dry run)* — packs but does not push; useful as a pre-flight (quickstart
  C1) but does not land the package. Not the publish itself.
- *Edit `release.yml`* — unnecessary; the path already exists and is correct. Editing it would
  expand scope and risk the SC-005 "no contract/CLI change" invariant for no benefit.

## Decision 2 — The single in-repo deliverable is a docs runbook, not a CI gate

**Decision**: The durable guardrail (FR-005) is a human-facing Markdown runbook at
`docs/release/contracts-version-bump-checklist.md`. No new automated CI check is added.

**Rationale**: The cross-repo issue explicitly suggests "adding a release checklist line," and
the spec Assumptions scope an automated source-ahead-of-feed check **out**. The existing
`contract-coherence` gate in `FS-GG/.github` already fails when the registry pin and SDD source
disagree; the missing piece the 042 gap exposed is a *procedural* reminder that a source bump
must also publish and advance `package-version` in the same change. A checklist is the
minimal, sufficient, in-scope artifact.

**Alternatives considered**:
- *An advisory CI job that flags fsproj `<Version>` > newest feed version* — genuinely useful
  and recorded as a future enhancement, but out of scope here (spec Assumptions) and would add
  a workflow + a feed query on every PR. Deferred.
- *Add the line to `docs/release/versioning-policy.md`* — rejected: that doc is an explicit
  *projection* of `specs/018` + `release-readiness.json` and must not gain new normative
  content (it states it is "never a second source of truth"). A standalone runbook avoids
  contaminating the projection.

## Decision 3 — Checklist home and shape: `docs/release/contracts-version-bump-checklist.md`

**Decision**: A new standalone doc under `docs/release/`, a numbered checklist keyed to the
three same-change actions, cross-linking the registry, the `contract-coherence` gate, ADR-0001,
and the feature-039 `release.yml` publish path.

**Rationale**: `docs/release/` already holds the release-facing docs (installation, versioning
policy, compatibility matrix). A maintainer bumping the contracts version looks there. Keeping
it standalone (not folded into versioning-policy) respects Decision 2's projection boundary and
matches the contracts-vs-product version split the policy doc already calls out
("Schema-version vs contract-version divergence").

**Alternatives considered**: a `CONTRIBUTING`/`RELEASING` root doc (rejected — release docs live
under `docs/release/`); inline in the spec only (rejected — specs are per-feature history, not a
durable maintainer runbook).

## Decision 4 — Registry `package-version` advance is cross-repo, performed after the feed confirms

**Decision**: SDD does not edit `FS-GG/.github` `registry/dependencies.yml`. After `1.1.0` is
confirmed live on the feed, the SDD side notifies the registry coordinator on FS-GG/.github#42
(or successor) to advance `fsgg-contracts.package-version 1.0.1 → 1.1.0`. The registry `version`
pin is already `1.1.0`.

**Rationale**: ADR-0001 makes `FS-GG/.github` the owner of the registry; cross-repo changes flow
through the issue protocol, not by editing another repo's file from here. FR-007 forbids
`package-version` running ahead of the feed, so the advance is strictly ordered **after** the
publish is verified (quickstart C-feed).

**Alternatives considered**: editing the registry from this repo (rejected — violates ADR-0001
ownership and the cross-repo protocol); advancing `package-version` speculatively at publish
time (rejected — FR-007, and a failed/rolled-back publish would leave the registry lying).

## Decision 5 — The in-repo 042 registry **fixture** stays frozen

**Decision**: Do not edit `tests/fixtures/registry/dependencies.yml` as part of this feature.

**Rationale**: That file is feature 042's validator **test input** (a snapshot of the real
registry the validator was written against), not the live registry. Editing it would change
042's golden expectations, which SC-005 forbids ("no schema/contract/CLI/golden byte changes").
Syncing the fixture to a newer registry snapshot, if ever wanted, is a 042 follow-up, not this
release-engineering feature. The live registry advance happens cross-repo (Decision 4).

**Alternatives considered**: bumping the fixture's `package-version` to `1.1.0` to "keep it
current" (rejected — couples a release-engineering publish to a validator golden change and
risks SC-005).

## Decision 6 — No new F# tests; verification is the publish run + feed query + doc presence

**Decision**: Add no F# unit tests. Verify via: (a) the publish workflow run landing `1.1.0` on
the feed, (b) a real feed query showing `1.1.0`, (c) the new runbook existing and naming the
three actions. The existing `FS.GG.Contracts.Tests` continues to gate the publish.

**Rationale**: No product behavior changes (Constitution VI), mirroring feature 039 which added
no F# test for an analogous release-engineering change. A Markdown runbook is verified by
presence/content, not by a unit test.

**Alternatives considered**: a doc-lint/presence test asserting the checklist exists (rejected as
over-engineering for a single maintainer doc; the quickstart presence check suffices).
