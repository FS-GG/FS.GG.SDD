# Contract: navigable early-stage result for `agents` / `refresh`

**Scope**: the behavior of `fsgg-sdd agents` and `fsgg-sdd refresh` when
`readiness/<id>/work-model.json` is **absent**. All other states are unchanged.

## Trigger

Exactly the existing missing-work-model branches:

- `agents`: `HandlersAgents.fs:211-212` (work-model snapshot `None`).
- `refresh`: the early all-blocked path `HandlersRefresh.fs:126-148` and the
  `"missing"`/`"blocked"` downstream arms `HandlersRefresh.fs:322-330`, when the
  absence is rooted in the missing work model.

## Before → after

| | Before (the gap) | After |
|---|---|---|
| Diagnostic | `agents.missingWorkModel` / `refresh.blockedUpstreamView` (**error**) | `agents.earlyStageGuidance` / `refresh.earlyStageGuidance` (**advisory**) |
| Outcome | `Blocked`, exit `1`, no usable next step | non-blocking, exit `0` (SC-002) |
| `NextAction` | generic "correct blocking diagnostics" | `earlyStageGuidance` → `.fsgg/early-stage-guidance.md` |
| Best-effort guidance | none | which of charter/spec/clarifications/checklist exist + the next lifecycle command, derived **only** from present artifacts |
| Label | — | explicitly **early-stage / partial** (FR-006) |
| On-disk view writes | none (was blocked) | **none** — no `guidance.json`/`commands.md`/`skills.md` (FR-008/FR-011) |

## Invariants

- **FR-008 / FR-011**: no on-disk view is written and nothing is digest-stamped as the
  full work-model projection. The best-effort guidance never fabricates facts about
  absent artifacts.
- **FR-006**: the early-stage result is unambiguously distinguished from the full
  guidance (the advisory diagnostic + `NextAction` ActionId + the explicit label).
- **Observability (VIII) / D3**: only the *missing* work model is reclassified.
  `agents.malformedWorkModel`, `agents.staleWorkModel`, `agents.blockedWorkModel`, and
  a malformed existing generated view still **block** (error, exit 1).
- **SC-006**: once the work model is buildable, both commands run the existing
  generators unchanged → byte-identical views; this branch is not taken.
- **SC-004**: the early-stage report is byte-identical across repeated runs.
- **FR-009**: identical behavior for the `claude` and `codex` targets.

## Report surface

- New diagnostics `agentsEarlyStageGuidance` / `refreshEarlyStageGuidance` in
  `CommandReports.fs(/.fsi)` (advisory severity; remedy text points to the seeded
  file). New `NextAction` ActionId `earlyStageGuidance`.
- Projects through the existing `CommandReport` three ways (default/`--json`,
  `--text`, `--rich`); `--rich` adds/drops no facts and degrades to zero ANSI.
- `PublicSurface.baseline` for `FS.GG.SDD.Commands` updated for the new constructors.
