# Feature Specification: `sdd` as the Default Lifecycle at the SDD Scaffolder, and the Spec-Kit→SDD Migration Path

**Feature Branch**: `item/597-scaffold-sdd-default`

**Created**: 2026-07-20

**Status**: Draft

**Input**: FS.GG.SDD#597 — "[cross-repo] fsgg-sdd scaffold default → sdd; ship spec-kit→sdd migration
path". Filed from `FS-GG/.github`, the SDD slice of the **ADR-0056** cross-repo epic (`.github#1245`).
Contract: `scaffold-provider` (v1). ADR-0056 (`.github#1244`) amends ADR-0002 §D2. Sibling slices:
`.github#1246` (Templates — flip the `fs-gg-ui` template `lifecycle.defaultValue` spec-kit→sdd,
publish-before-flip) and `.github#1247` (`.github` — `skills.yml`/`workRoadmap` predicate widening).
This item **blocks** the epic's capture-removal work.

## Overview

**ADR-0056** makes `sdd` the one default lifecycle across the platform and freezes the `spec-kit` lane
for scheduled removal. The org decision is **already accepted** (`.github#1244` closed); this feature is
the **SDD-repo realization** of two of the epic's deliverables:

- **A. The scaffolder default.** After the flip, `fsgg-sdd scaffold` — invoked with no explicit
  lifecycle override — must produce an **`sdd`-lane** workspace.
- **B. The migration path.** Grandfathered `spec-kit`-lane trees must have a documented, tool-assisted
  way to move onto the `sdd` lane **before** the (human-set, not-yet-scheduled) removal milestone.

### The constraint that shapes deliverable A (FR-002 / FR-004, unchanged)

Generic SDD embeds **no** provider-specific package id, template id, path, docs URL, **or parameter
value** (`scaffold` FR-002 / SC-005; the effective-parameters design FR-003 / FR-004). "Make
`fsgg-sdd scaffold` default to `sdd`" therefore **cannot** mean SDD hard-codes an `sdd` literal, a
`lifecycle` default, or any lane knowledge — that would re-introduce exactly the provider coupling the
scaffold contract was built to keep out.

The value-agnostic realization is the one the contract already provides. `lifecycle` is a
**provider-declared parameter**: its default lives in the `fs-gg-ui` template's `template.json`
(`lifecycle.defaultValue`), and `fsgg-sdd scaffold` already resolves provider-declared
`parameters[].default`s — overlaid by author `--param` overrides, author-wins — into the additive
`effectiveParameters` field on `.fsgg/scaffold-provenance.json` and the scaffold report. So the flip is
authored **once**, in the template (`.github#1246`), and `fsgg-sdd scaffold` **forwards** it. SDD's owned
change is not to *choose* `sdd`; it is to **pin, with a mutation-checked witness, that scaffold faithfully
forwards whatever lifecycle default the provider declares** — so a fresh, override-free scaffold against
the flipped `fs-gg-ui` template records `lifecycle: sdd`, and a future regression that dropped or
overrode the forwarding goes red. Deliverable A embeds a *mechanism* test, never the value.

### Publish-before-flip: what A can and cannot assert here (ADR-0037 over the `scaffold-provider` contract)

The `fs-gg-ui` template flip is `.github#1246`, and until that provider version publishes, no committed
provider declares `lifecycle.defaultValue: sdd`. Deliverable A's **value** assertion (an override-free
`fs-gg-ui` scaffold yields `lifecycle: sdd`) is therefore **gated on `.github#1246`** and belongs to a
follow-up implementation PR sequenced after it. What is **unblocked now** is the value-agnostic mechanism
witness — that scaffold forwards a *synthetic* provider's declared `lifecycle` default verbatim into
`effectiveParameters`, whatever that default's value — because it names no real provider and no `sdd`
literal. This feature keeps the two apart so SDD never carries a `sdd` value ahead of the provider that
owns it.

### What deliverable B is, and is not

The existing `docs/migration-from-spec-kit.md` maps a **standard Spec Kit** project onto native SDD
sources **additively** — keep `specs/`/`.specify/`, add SDD alongside. That is a *different* scenario
from ADR-0056's: a `spec-kit`-**lane** tree (one an earlier `fsgg-sdd scaffold`/`dotnet new fs-gg-ui`
produced with `lifecycle: spec-kit`) that must move onto the **`sdd` lane** before the lane is removed.

The tool primitive for the assisted move already exists: `fsgg-sdd upgrade`'s `artifactReSeed` step is a
**no-clobber re-materialization of the missing seeded SDD skeleton** via `init`'s `AgentGuidanceTarget`
effects (`docs/reference/doctor-upgrade.md`). So the migration path is **re-supply via `upgrade`** (or a
clean **re-scaffold** on the `sdd` lane), authored as an SDD-owned guide — not a new command. No `spec-kit`
content is deleted or rewritten; the move is additive and safe to re-apply, exactly as the existing guide
is.

## Clarifications

### Session 2026-07-20

- **Q1 — Doctor-guard scope.** Should `fsgg-sdd doctor` gain a fail-closed "`sdd`-lane, no skeleton"
  check, or is that guard entirely template-side? → **Template-side only.** An `fsgg-sdd scaffold`-produced
  `sdd`-lane tree is never lifecycle-less (skeleton always seeded), and a `doctor` lane check would have
  to read a template/lifecycle marker — embedding lane knowledge into generic SDD, which FR-001/FR-004
  forbid. The raw-`dotnet new` consumer's notice/readiness guard lives in the `fs-gg-ui` template
  (`.github#1246`). Recorded in Out of Scope.
- **Q2 — Migration-guide home.** New sibling doc, or a section in `docs/migration-from-spec-kit.md`? →
  **New sibling doc** (`docs/migrate-spec-kit-lane-to-sdd.md`), cross-linked with the existing guide. The
  lane-switch narrative ("leave the removed lane before the deadline") contradicts the existing guide's
  additive framing ("Spec Kit remains a valid workflow"); one file cannot carry both without confusing
  the reader. Recorded in FR-005 / AC-005.

## Requirements

### Functional

- **FR-001**: `fsgg-sdd scaffold` MUST embed no `sdd` literal, no `lifecycle` default, and no lane
  knowledge. The lifecycle default MUST remain a **provider-declared** parameter that scaffold forwards
  value-agnostically (preserves `scaffold` FR-002 / FR-004). The behavior change ADR-0056 asks for MUST
  be delivered by the provider flip (`.github#1246`) plus SDD's faithful forwarding — never by an SDD
  value embed.
- **FR-002**: `fsgg-sdd scaffold`, run with no `--param lifecycle=…` override against a provider whose
  descriptor declares a `lifecycle` parameter with a default, MUST record that default verbatim as the
  effective `lifecycle` value in `.fsgg/scaffold-provenance.json` `effectiveParameters` and in all three
  report projections — for **any** declared default value, `sdd` included, with no value special-cased.
- **FR-003**: An author `--param lifecycle=<v>` override MUST continue to win over the provider-declared
  default (author-always-wins, `scaffold` FR-004), so a consumer can still choose `spec-kit` or `none`
  during the grandfather window without SDD asserting a preferred value.
- **FR-004**: The change MUST be a monotone extension of the `scaffold-provider` contract: no
  `scaffold-provenance` schema bump (stays v1), and no scaffold invocation valid before this feature may
  become invalid after it. `--provider` remains **required**; this feature adds no default *provider*
  and does not relax `scaffold.providerMissing`.
- **FR-005**: SDD MUST ship a **new, standalone migration guide** — `docs/migrate-spec-kit-lane-to-sdd.md`,
  a sibling of `docs/migration-from-spec-kit.md` (not a section folded into it, per Clarification Q2) —
  for moving a `spec-kit`-lane scaffolded tree onto the `sdd` lane, grounded in the existing tool
  primitives: `fsgg-sdd upgrade` (no-clobber re-supply of the missing SDD skeleton) or a clean re-scaffold
  on the `sdd` lane. The guide MUST be **additive and non-destructive** (no deletion/rewrite of existing
  `specs/`/`.specify/` or authored content) and safe to re-apply. The two guides MUST **cross-link**: the
  lane-switch guide keeps the deprecation framing (leave the removed lane before the deadline) distinct
  from the additive-adopt framing (Spec Kit remains valid) that would contradict it if merged.
- **FR-006**: The migration guide MUST embed no provider-specific package id, template id, path, or docs
  URL (the same generic-SDD constraint as the scaffold contract); it refers to lifecycle **lanes** and
  the generic `upgrade`/`scaffold`/`init` verbs, not to `fs-gg-ui` or any Rendering artifact.

### Acceptance Criteria

- **AC-001**: A `scaffold` run against a **synthetic** provider that declares `lifecycle` with
  `default: sdd` and no `--param` override records `lifecycle=sdd` in `effectiveParameters` (json/text/
  rich) and in `scaffold-provenance.json`. (Mechanism witness — names no real provider; **unblocked**.)
- **AC-002**: The same synthetic provider with `default: spec-kit` records `lifecycle=spec-kit`, and with
  `--param lifecycle=none` records `lifecycle=none` — proving the value is forwarded, never chosen, and
  the override still wins (FR-002 / FR-003).
- **AC-003**: `scaffold` with `--provider` omitted still blocks with `scaffold.providerMissing` (exit 1),
  unchanged — no default provider is introduced (FR-004).
- **AC-004** *(gated on `.github#1246`; follow-up PR)*: An override-free `fsgg-sdd scaffold --provider
  <fs-gg-ui>` against the **published, flipped** template records `lifecycle=sdd`. This is the end-to-end
  value witness and is asserted only once the provider flip has published (publish-before-flip).
- **AC-005**: `docs/migrate-spec-kit-lane-to-sdd.md` exists, documents the `spec-kit`-lane → `sdd`-lane
  move via `upgrade` re-supply and via re-scaffold, states the additive/non-destructive/re-appliable
  guarantees, names no provider-specific literal, and cross-links `docs/migration-from-spec-kit.md` (and
  vice versa). A doc-lint / link check over both passes.

## Out of Scope

- **The `fs-gg-ui` `template.json` `lifecycle.defaultValue` flip** — Templates repo, `.github#1246`
  (publish-before-flip). SDD forwards the flipped default; it does not author it.
- **`skills.yml` / `driver-skill-manifest.json` / `workRoadmap` predicate widening** — `.github#1247`.
- **The lifecycle-less-tree guard is entirely template-side** (resolved, Clarification Q1). The
  post-scaffold notice + readiness check that keeps a raw-`dotnet new fs-gg-ui` standalone consumer from
  silently getting a lifecycle-less `sdd`-lane tree lives in the `fs-gg-ui` template (`.github#1246`). No
  new `fsgg-sdd doctor` check is added: an `fsgg-sdd scaffold`-produced `sdd`-lane tree is **never**
  lifecycle-less (the SDD skeleton is always seeded via `init`'s effects), and for `doctor` to guard the
  raw-template consumer it would have to detect the "`sdd` lane" from a template marker — embedding lane
  knowledge into generic SDD, the exact coupling FR-001/FR-004 forbid. Should such a check ever be
  wanted, it is a separate feature, not this one.
- **The `spec-kit` lane removal itself, its removal `Target` date, and the capture-removal work** — the
  date is a human-set board field on the removal epic; removal is downstream of and blocked by this item.
- **Any `scaffold-provenance` schema change** — the design is additive over v1.
