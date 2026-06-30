# Phase 1 Data Model: Early-Stage Agent Guidance Bootstrap

This feature adds **one authored Markdown artifact** and reclassifies **one state
transition** in two commands. It introduces no new structured (JSON) schema and no new
public type beyond report-diagnostic constructors. Markdown is an authoring surface
(Constitution II); the machine contract is unchanged.

## Entities

### 1. Early-stage authoring guidance (`.fsgg/early-stage-guidance.md`)

A generic, deterministic, no-clobber authored skeleton file. Independent of any work
item's work model.

| Aspect | Value |
|---|---|
| Path | `.fsgg/early-stage-guidance.md` |
| Producer | `fsgg-sdd init` (`Foundation.initEffects`) — one new `WriteFile` |
| Write kind | `ArtifactWriteKind.AgentGuidanceTarget` (no-clobber via `canOverwrite`) |
| Content source | New embedded literal `earlyStageGuidanceText` (`Foundation.fs`) |
| Provenance | Authored (report ownership `"authored"`); not `generatedProduct`; not a release-catalog entry |
| Determinism | No date/timestamp/random/repo/provider token → byte-identical every run |

**Required body sections** (one block per pre-work-model stage, plus the contracts):

- **Per stage** (`charter`, `specify`, `clarify`, `checklist`): the `fsgg-sdd`
  command that runs the stage; the required section headings the stage's artifact must
  contain (verbatim the live standard-section list); the stable-id formats the artifact
  uses.
- **Authoring contracts**: the **§1.1 acceptance coverage line** rule (the
  `- FR-###: … (AC-###)` strict-scan form) and the **§1.2 `evidence.yml`** satisfaction
  rule (satisfied only by `result: pass` **and** `synthetic: false`), restated from
  `docs/reference/authoring-contracts.md` — never redefined.
- **Lifecycle pointer**: that `charter → specify → clarify → checklist` precede the
  work model, and that once `verify`/`ship` builds the work model, the generated
  `readiness/<id>/agent-commands/<target>/` views become authoritative (so the static
  guidance does not shadow them — FR-008, edge case "work model now exists").

**Invariants**: every heading / id prefix / command / path / contract rule named must
resolve against the live contract (FR-007 / SC-003), enforced by the drift-guard test.

### 2. Per-work-item generated guidance (existing — unchanged)

`readiness/<id>/agent-commands/<target>/{guidance.json,commands.md,skills.md}`,
digest-stamped from `work-model.json` (`HandlersAgents.agentGuidanceManifestJson`,
`generated: true`, source digest). Remains the sole source of truth once the work model
is buildable. This feature does not touch the buildable path → byte-identical output
(SC-006).

### 3. Early-stage command result (new disposition of `agents` / `refresh`)

The actionable, clearly-bounded outcome when `work-model.json` is **absent**. Carried
entirely in the `CommandReport`:

| Field | Early-stage value |
|---|---|
| Diagnostic | `agents.earlyStageGuidance` / `refresh.earlyStageGuidance` — **advisory** severity (non-blocking) |
| Outcome | not `Blocked`; exit code `0` (actionable, never a dead end — SC-002) |
| Best-effort facts | which of charter/spec/clarifications/checklist exist, and the next lifecycle command — derived **only** from present artifacts (FR-011) |
| Label | explicitly **early-stage / partial** so it is never mistaken for the full projection (FR-006) |
| `NextAction` | `earlyStageGuidance` ActionId, routing the author to `.fsgg/early-stage-guidance.md` |
| On-disk writes | **none** (no `guidance.json`/`commands.md`/`skills.md`) — preserves FR-008/FR-011 |
| Digest stamping | none — the best-effort guidance is never digest-stamped as the full work-model projection |

## State transition: missing work model

```text
fsgg-sdd agents | refresh, with readiness/<id>/work-model.json …

  ABSENT      ─ before ─▶  Blocked: agents.missingWorkModel / refresh.blockedUpstreamView
                          (error, exit 1, no usable next step)          ← the reported gap

  ABSENT      ─ after  ─▶  Early-stage navigable:  *.earlyStageGuidance (advisory, exit 0),
                          best-effort facts from existing artifacts, NextAction → guidance file
                          (FR-004 / FR-005 / FR-010b)

  MALFORMED   ─ before & after ─▶  Blocked: agents.malformedWorkModel (error)  (unchanged — FR-008)
  STALE       ─ before & after ─▶  Blocked/advisory as today                   (unchanged)
  BUILDABLE   ─ before & after ─▶  full generated views, byte-identical        (unchanged — SC-006)
```

## Stable-id / heading facts the guidance encodes (read-only mirror)

| Stage | Command | Required headings (verbatim live list) | Id formats |
|---|---|---|---|
| charter | `fsgg-sdd charter` | Identity, Principles, Scope Boundaries, Policy Pointers, Lifecycle Notes | front-matter (`workId`, `stage`, `changeTier`, …) |
| specify | `fsgg-sdd specify` | User Value, Scope, Non-Goals, User Stories, Acceptance Scenarios, Functional Requirements, Ambiguities, Public Or Tool-Facing Impact, Lifecycle Notes | `US-###`, `AC-###`, `FR-###`, `SB-###`, `AMB-###` |
| clarify | `fsgg-sdd clarify` | Source Specification, Clarification Questions, Answers, Decisions, Accepted Deferrals, Remaining Ambiguity, Lifecycle Notes | `CQ-###`, `DEC-###`, `AMB-###` |
| checklist | `fsgg-sdd checklist` | Source Specification, Source Clarifications, Source Snapshot, Checklist Items, Review Results, Accepted Deferrals, Blocking Findings, Advisory Notes, Lifecycle Notes | `CHK-###`, `CR-###` |

All four heading lists and all id prefixes are pinned to their live sources by the
drift-guard test, so this table cannot silently drift from the parser.
