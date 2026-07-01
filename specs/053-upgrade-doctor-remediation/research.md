# Phase 0 Research: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

Feature: `053-upgrade-doctor-remediation` · Date: 2026-07-01

Each decision below resolves a spec-open planning choice. Format: **Decision / Rationale /
Alternatives considered**. Two decisions (R4, R6) and one confirmation (R5) are routed to
`/speckit-clarify` before `/speckit-tasks`; each states a recommended resolution so the plan is
complete and buildable if clarify affirms it.

---

## R1 — `doctor` and `upgrade` are two peer cross-cutting commands with their own staged drivers

**Decision**: Add `Doctor` and `Upgrade` as new `SddCommand` cases, each with
`nextLifecycleCommand = None` (like `Scaffold`/`Refresh`/`Agents`), each dispatched to its own staged
driver in `nextLifecycleEffects` (peers of `computeScaffoldNext`). They are reachable only via the
CLI and never appear in a lifecycle command path (FR-001/FR-006).

**Rationale**: Matches the established cross-cutting shape exactly — `Scaffold` already bypasses the
generic write-once lifecycle guard and runs a bespoke multi-tick driver that re-derives its phase
from `model.InterpretedEffects`. `doctor` (read-only) and `upgrade` (multi-step, confirmable) both
need that bespoke staging, so they follow the same pattern rather than the `Charter…Ship` generic
path.

**Alternatives considered**: Folding remediation into `refresh` — rejected: `refresh` regenerates
SDD-owned *generated views* and explicitly does **not** re-seed or self-update (feature 052 D8;
CLAUDE.md), and ADR-0009 is emphatic that remediation is a distinct explicit verb, never a side
effect (FR-008).

## R2 — The required minimum is read from declarative truth, reusing feature 052

**Decision**: `doctor`/`upgrade` compute the CLI axis exactly as 052's scaffold coherence does:
resolve the provider descriptor from `.fsgg/providers.yml` (`parseProviderRegistry`, keyed by the
`ProviderName` recorded in `.fsgg/scaffold-provenance.json`), read the provider-declared
`MinimumCliVersion` (the nested `minimumFsggSdd.version` scalar) and/or the provenance-recorded
`RequiredMinimumCliVersion`, and compare against the installed `request.GeneratorVersion.Version`
via `Fsgg.Version.compare`. When the recorded and live values disagree, evaluate against the **live**
descriptor minimum (spec Assumption). The value embedded in the binary is never treated as the truth
(FR-003).

**Rationale**: Reuses the shipped, tested 052 primitives (`Fsgg.Version`, the provider-registry
parse, the `cliBehindMinimum` semantics) with zero new version machinery; keeps SDD value-agnostic.

**Alternatives considered**: A doctor-specific version reader — rejected as duplication and a second
source of truth.

## R3 — The expected seeded-artifact set is `SeededSkills.skillNames` × two surfaces + early-stage guidance

**Decision**: The artifact axis compares, per path, expected-vs-present for exactly the SDD-owned
seeded skeleton: the 15 `fs-gg-sdd-*` skills under both `.claude/skills/<name>/SKILL.md` and
`.codex/skills/<name>/SKILL.md` (the `Internal.SeededSkills.skillNames` list is the single in-code
source), plus `.fsgg/early-stage-guidance.md`. Missing paths are named in the report (FR-004); the
constitution and `AGENTS.md`/`CLAUDE.md` are already no-clobber init outputs but are out of scope for
"seeded artifacts drift" naming per the spec's enumerated set.

**Rationale**: `SeededSkills` is already the authoritative, drift-guarded set reused by
`init`/`scaffold`; `doctor` reads presence and `upgrade` re-materializes the missing subset with the
identical `initEffects` no-clobber writes (R8).

**Alternatives considered**: Hard-coding the expected list in a doctor module — rejected (duplication;
would drift from the seeding source).

## R4 — Self-update ↔ re-seed binary identity **[CLARIFY]**

**Problem**: A `dotnet tool update` self-update only takes effect on the **next** invocation (spec
Assumption). So within one `upgrade` run, the re-seed step still executes the **running** (pre-update)
binary's embedded skeleton — which, for a genuinely-behind CLI, may lack a skill the newer binary
would seed. US2-AC2 wants a *subsequent* `doctor` to report fully coherent.

**Recommended decision (to confirm in clarify)**: Within one `upgrade`, the re-seed step
materializes the **running binary's** embedded skeleton via the in-process `init` effects
(no-clobber, R8). When a self-update was also applied in the same run, the report's `UpgradeSummary`
honestly records that the newly installed binary's *additional* seeded artifacts (if any) are
reconciled by the next `doctor`/`upgrade` run under the new binary — i.e. the run is reported as
"self-update applied; re-seed applied for the current binary's set; re-run `doctor` to confirm",
never as a false "fully coherent" (FR-013). The subsequent `doctor` (new binary) then shows and, if
needed, a second `upgrade` re-seeds the remainder. This keeps the current run deterministic and
avoids re-exec complexity.

**Alternatives considered**: (a) Re-invoke the just-installed tool for the re-seed step at the
`RunProcess` edge (`dotnet tool run fsgg-sdd <reseed>`) so the new bytes are used in the same run —
more faithful to a one-shot "fully coherent" outcome but adds a re-exec + a re-entrant CLI contract
and non-determinism; deferred unless clarify demands single-run full coherence. (b) Refuse to
self-update and re-seed in the same run (force two invocations) — simpler but worse UX and arguably
violates US2-AC1's "each … applied" in one `upgrade`.

## R5 — Diff-rendering fidelity **[CONFIRM]**

**Decision (recommended)**: "Shown as a diff" is rendered per step-kind as a compact
**before/after preview**, not a full unified-diff engine: self-update → `installed X → target ≥Y`
(a version delta); re-seed → `+ <path> (new, N bytes)` per created file (no prior content); re-pin →
a minimal changed-line before/after of `.fsgg/providers.yml`. This satisfies "each shown as a diff
and confirmed" (FR-007) while honoring idiomatic simplicity (Principle IV).

**Rationale**: Re-seed writes are file *creations* (no-clobber never overwrites), so a full diff is
degenerate; only re-pin edits an existing file, and a small changed-line preview is sufficient and
deterministic. Rich may present these in a panel/table; the json carries the same structured facts.

**Alternatives considered**: A full LCS/unified-diff library — rejected as unjustified complexity for
a preview that is almost always file-creation or a one-line pin bump. If clarify wants richer re-pin
diffs, a bounded line-diff can be added without changing the report contract.

## R6 — Template re-pin scope **[CLARIFY]**

**Problem**: ADR-0009 lists "template re-pin" as one of the three steps, but (a) the Templates/registry
half of epic-#85 has not shipped, (b) generic SDD may embed no provider-specific template
id/version/source/docs-URL (constitution; CLAUDE.md; FR-002/SC-005 of feature 052), and (c) this
feature is scoped as "the remediation half only" and "No registry surface change" (spec Assumptions).
There is today no value-agnostic *signal* that says "the consumer's template pin is behind".

**Recommended decision (to confirm in clarify)**: Model re-pin as a **recognized, value-agnostic,
usually-inert** step. `upgrade` writes only the consumer-owned `.fsgg/providers.yml` (R9), and only
when a template-version drift signal is available from the resolved provider descriptor
value-agnostically (e.g. a declared/available template version that differs from the consumer's
pinned one). Absent such a signal it reports the re-pin step as `noTarget` (nothing to reconcile on
this axis) — the CLI-axis + artifact-axis remediation still fully deliver US1/US2. Full template
re-pin lands with the Templates half of epic-#85. SDD embeds no template literal in either case.

**Rationale**: Keeps 053 independently shippable now (like 052 shipped ahead of the Templates half),
delivers the two axes that *are* fully specified and testable (behind-CLI, missing-artifacts), and
never puts provider specifics into generic SDD. The `UpgradeSummary` shape already carries a per-step
outcome, so a later Templates-half feature can light up re-pin without a contract change.

**Alternatives considered**: (a) Implement a concrete re-pin now — rejected: would require SDD to
know provider template identity/versions (constitution violation) and depends on unshipped Templates
work. (b) Drop re-pin from the command entirely — rejected: ADR-0009 names three steps and the
`UpgradeSummary` should be forward-compatible; better to recognize the step and report `noTarget`.

## R7 — Confirmation is a new `Confirm` effect with interactivity threaded into the request

**Decision**: Add `Confirm of stepId: string * prompt: string` to `CommandEffect` and a
`Confirmed: bool option` field to `CommandEffectResult`. The edge interpreter resolves `Confirm` by
reading a line from `Console.In` **only** when the run is interactive; the pure `update` re-derives
which step to present next from the interpreted-effect log (the `Confirmed` outcomes), exactly like
scaffold re-derives its tick. Interactivity and the explicit apply flag are threaded into
`CommandRequest` as `IsInteractive: bool` and `AssumeYes: bool`, computed at the edge
(`Console.IsInputRedirected` — a **new** input-interactivity signal; `detectCapabilities` today only
checks output redirection) and from `--yes` parsing in `Program.fs`.

**Rationale**: Constitution V requires stateful/I/O workflows to expose an MVU boundary with `Effect`
for requested I/O and a pure transition; a confirmation is exactly that. Threading interactivity into
the request lets the pure core decide *up front* to refuse (non-interactive, no `--yes`) with zero
writes and no prompt-hang (FR-012/SC-004), rather than emitting a `Confirm` that would block.

**Alternatives considered**: Ad-hoc `Console.ReadLine` inside `Program.fs`/the edge outside the
effect model — rejected (hides a transition + I/O from `update`, unt­estable via the model harness,
violates Principle V). Reusing `RunProcess` for prompts — rejected (semantically wrong; no process).

## R8 — No-clobber re-seed reuses `init`'s seeding effects verbatim

**Decision**: The re-seed step replays the relevant subset of `initEffects request` — the
`SeededSkills.skillEffects` and the `.fsgg/early-stage-guidance.md` `WriteFile`, all carrying
`AgentGuidanceTarget` — through the existing edge interpreter. `canOverwrite` already returns `true`
only for absent or byte-identical targets and refuses a differing present `AgentGuidanceTarget`, so
re-seed materializes **only** the missing artifacts and never overwrites an author-edited one
(FR-010, US4-AC2) with **no new write logic**.

**Rationale**: This is precisely the semantics FR-010 asks for, and it is already implemented and
tested for `init`/`scaffold`. Reuse over reinvention (Principle IV; feature 052 D8 confirmed the
re-seed path is `init`'s effects, not `refresh`).

**Alternatives considered**: A doctor/upgrade-specific writer — rejected (duplicates no-clobber logic;
risks divergence from the seeding source of truth).

## R9 — Re-pin writes only the consumer-owned `.fsgg/providers.yml`; write kind/policy

**Decision**: The re-pin step's only write target is the scaffold's own `.fsgg/providers.yml`
(FR-009, SC-005); governed registry / provider-descriptor state is never touched. Because
`.fsgg/providers.yml` is author/provider-owned and a present differing `StructuredSource` is refused
by `canOverwrite`, re-pin needs an explicit overwrite path: either a dedicated write kind for
"consumer-owned config the author has authorized rewriting via confirmation", or the existing
`AllowGeneratedRefresh`-style `OverwritePolicy` gate applied to this one confirmed write. The write is
still gated behind the per-step `Confirm`/`--yes`, so it is never silent.

**Rationale**: Keeps the ownership boundary crisp (only the consumer pin, surfaced as a reviewable
diff) and reuses the existing overwrite-policy machinery rather than inventing a bypass.

**Alternatives considered**: Force-writing regardless of policy — rejected (breaks the no-silent-write
invariant and the `canOverwrite` contract). *Note*: this decision is only exercised if R6's re-pin
step has a target; when re-pin reports `noTarget`, no write and no policy change is needed.

## R10 — Exit-code taxonomy mirrors scaffold; `upgrade.*` step defects escalate to exit 2

**Decision**: `doctor` exits 0 whenever it produces a report (including with drift present, FR-002).
`upgrade` exits 0 on success or a clean no-op (already coherent), **1** on a user-input refusal
(non-interactive without `--yes`, FR-012), and **2** on a step defect (a confirmed self-update/re-pin/
re-seed that failed to apply, FR-013/SC-006). Implement by adding the `upgrade.*` step-defect
diagnostic ids (e.g. `upgrade.stepFailed`, `upgrade.selfUpdateFailed`) to the existing
`providerDefectIds` exit-2 set in `exitCodeForReport`; the refusal diagnostic stays in the default
Blocked→exit-1 class.

**Rationale**: Mirrors the shipped scaffold user-input-vs-defect split (`exitCodeForReport` already
keys exit 2 off a defect-id set), so the taxonomy is consistent and the incomplete-never-complete
guarantee is a report-outcome property, not ad-hoc exit logic.

**Alternatives considered**: A bespoke exit-code function for upgrade — rejected (duplicates the
existing outcome→exit mapping; risks divergence).

## R11 — Additive three-projection report blocks (`DoctorSummary`, `UpgradeSummary`)

**Decision**: Add `DoctorSummary` and `UpgradeSummary` as optional fields on `CommandReport`
(like `ScaffoldSummary`), each with `CommandModel` mirror fields, deterministic json emit
(`CommandSerialization`), a plain-text projection (`CommandRendering`), and automatic rich derivation.
Rich adds/drops no fact and changes no json byte (FR-014/SC-007); rich degrades to zero-ANSI when
non-interactive/redirected or color-disabled.

**Rationale**: The report is projected once and rendered three ways by the existing `resolve`
pipeline; adding two optional blocks is the established additive pattern (matches how 052 added
`RequiredMinimumCliVersion` to the scaffold block) and keeps the json automation contract stable.

**Alternatives considered**: Emitting free-form text — rejected (json is the machine contract,
Principle II).

## R12 — Degradation: no provenance / no minimum

**Decision**: With **no** `.fsgg/scaffold-provenance.json` (a bare `init` skeleton or plain repo),
both commands report "no scaffold provenance — nothing to reconcile", write nothing, exit 0
(FR-015, edge case). When the provider declares **no** minimum, neither command asserts CLI
staleness — `doctor` reports coherent-by-absence for the CLI axis (still reporting artifact
presence) and `upgrade` asserts no CLI version target (FR-016) — consistent with feature 052's
no-minimum handling. When the CLI installed version is itself unparseable, the CLI axis is skipped
honestly (052 D7), never asserting a false ordering.

**Rationale**: Directly encodes the spec's Edge Cases and the FR-015/FR-016 degradation contract;
reuses 052's honest-degradation semantics (`Fsgg.Version.compare` returns `None` when either side is
unparseable).

**Alternatives considered**: Blocking when provenance is absent — rejected (contradicts the
"always-safe" doctrine and FR-015).
