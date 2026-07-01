# Implementation Plan: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

**Branch**: `053-upgrade-doctor-remediation` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/053-upgrade-doctor-remediation/spec.md`

## Summary

Deliver the **remediation half** of ADR-0009 as two new cross-cutting commands that sit beside
`scaffold`/`refresh`/`agents` (`nextLifecycleCommand = None`), both projecting the same
`CommandReport` three ways (json / text / rich):

- **`fsgg-sdd doctor`** — a strictly read-only drift report: installed CLI vs the pin's required
  minimum (reusing feature 052's declarative-truth reading), which seeded skeleton artifacts are
  present vs expected, and a dry-run preview of what `upgrade` would change. It **never writes** and
  exits 0 whenever it reports.
- **`fsgg-sdd upgrade`** — the reconciliation verb across up to three steps (CLI self-update,
  template re-pin, missing-artifact re-seed), where **each** step is shown as a diff and applied
  only after per-step confirmation (or all at once under an explicit `--yes` flag). It is the
  **only** command permitted to mutate the CLI installation or consumer artifacts for remediation.

The single largest new capability is **interactive per-step confirmation**: the CLI is today a
batch pipeline (`init → interpret-until-idle → build-report → render → exit`) whose edge
interpreter never reads stdin. `upgrade` adds a constitutionally-clean confirmation loop — a new
`Confirm` effect resolved at the edge and re-derived-from-the-log staging in the pure `update`,
mirroring how `scaffold` already re-derives its phase from the interpreted-effect log.

See [research.md](./research.md) for the resolved design decisions (R1–R12),
[data-model.md](./data-model.md) for the new/extended entities, and [contracts/](./contracts/) for
the command and effect contracts.

> **⚠ Two scope forks surfaced during planning (see research.md R4, R6), to be confirmed in
> `/speckit-clarify` before `/speckit-tasks`:**
> 1. **Template re-pin mechanism (R6).** ADR-0009's three steps include "template re-pin", but the
>    Templates/registry half of epic-#85 has not shipped, and generic SDD may embed no
>    provider-specific template id/version/source (constitution; CLAUDE.md). The recommended
>    resolution treats re-pin as *value-agnostic and currently a recognized-but-usually-inert step*
>    that rewrites only the consumer's `.fsgg/providers.yml` when a template-version drift signal is
>    available, and otherwise reports "no re-pin target" — full re-pin lands with the Templates half.
> 2. **Self-update ↔ re-seed binary identity (R4).** A self-updated binary only takes effect on the
>    *next* invocation (spec Assumption). The recommended resolution: within one `upgrade`, re-seed
>    materializes the **running** binary's embedded skeleton (in-process `init` effects, no-clobber),
>    and when a self-update was also applied the report honestly notes any artifacts the newly
>    installed binary adds are reconciled on the next `doctor`/`upgrade` run — never reporting an
>    incomplete reconciliation as complete (FR-013).

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: `System.Text.Json` (deterministic hand-ordered serialization, existing
pattern); YamlDotNet-backed `parseProviderRegistry` (provider registry, reused from scaffold);
`Fsgg.Version` shared grammar (feature 052, reused for the CLI-vs-minimum compare); Spectre.Console
(rich projection only); `System.Diagnostics.Process` at the existing `RunProcess` edge (self-update
via `dotnet tool update`). BCL `Console.In`/`Console.IsInputRedirected` for the confirmation edge.
No new third-party dependency.

**Storage**: Files only. Read: `.fsgg/scaffold-provenance.json` (SDD-owned, feature 052 fields),
`.fsgg/providers.yml` (author/provider-owned registry), the seeded skeleton subtrees
(`.claude/skills/**`, `.codex/skills/**`, `.fsgg/early-stage-guidance.md`). Write (`upgrade` only):
the consumer-owned `.fsgg/providers.yml` (re-pin) and the missing seeded skeleton paths (re-seed).
No database. Governed registry / provider-descriptor state is never written.

**Testing**: xUnit (`open Xunit`); real filesystem fixtures (no mocks) — behind/coherent/no-min
scaffolds under `tests/fixtures/`; golden/byte-stable JSON for the two new report blocks; a scripted
stdin harness for the interactive confirmation loop and a non-interactive-refusal case;
`PublicSurface.baseline` surface guards for the extended `FS.GG.SDD.Commands` public surface; a
write-audit test proving `doctor` makes zero writes and `upgrade` writes only the two allowed
targets (SC-001/003/005).

**Target Platform**: Cross-platform CLI (`fsgg-sdd`) + libraries.

**Project Type**: Multi-project single solution (CLI + libraries). Not web/mobile.

**Performance Goals**: N/A. Determinism (byte-stable json across projections; stable exit-code
taxonomy) is the hard constraint, not throughput.

**Constraints**: `doctor` strictly read-only (zero writes, always exit 0 when it reports);
`upgrade` mutates only via explicit confirmation or `--yes`, never implicitly and never as a side
effect of any other command; no-clobber re-seed (never overwrite a present/author-edited artifact);
consumer-only writes (never governed state); value-agnostic (no provider-specific template
id/version/path/docs-URL in generic SDD); non-interactive without `--yes` refuses with zero writes
and no prompt-hang; rich degrades to zero-ANSI; json byte-identical across all three projections;
exit-code taxonomy mirrors scaffold (0 success/no-op, 1 user-input refusal, 2 step defect).

**Scale/Scope**: Two new `SddCommand` cases with full plumbing (parse/name/stage/next/help), two new
`CommandReport` summary blocks (`DoctorSummary`, `UpgradeSummary`) with json+text emit and rich
derivation, one new `Confirm` effect + edge interpreter case + interactivity threading, two new
staged drivers in `nextLifecycleEffects`, a small pure drift-computation module reused by both
commands, a family of `doctor.*`/`upgrade.*` diagnostics, one exit-2 defect-id addition, plus docs
and fixtures. **NEEDS CLARIFICATION** on the two scope forks above (R4, R6) and on diff-rendering
fidelity (R5).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Impl | **PASS** | `.fsi` updated for the new `Confirm` effect + result field, `CommandTypes` (two commands, two summaries), `CommandReports`; FSI/prelude exercise of the new pure drift module before `.fs` hardens; tests written fail-first. |
| II. Structured artifacts are the machine contract | **PASS** | `doctor`/`upgrade` **read** existing structured contracts (provenance v1, provider registry) and the report json is the machine contract; the consumer-owned `.fsgg/providers.yml` is the only re-pin target and its rewrite is a reviewable diff. No new persisted schema (report blocks are additive to the existing `CommandReport`). |
| III. Visibility in `.fsi` | **PASS** | All new public surface lands in `.fsi`; baselines refreshed for `FS.GG.SDD.Commands` (and `FS.GG.SDD.Artifacts` if diagnostics move there). |
| IV. Idiomatic simplicity | **PASS** | Records + DUs + pure functions; the staged drivers reuse the existing "re-derive phase from the interpreted-effect log" idiom (no new state field). One justified new effect (`Confirm`) — see Complexity Tracking; no advanced-feature machinery. |
| V. Elmish/MVU boundary | **PASS** | The confirmation I/O is modelled as an `Effect` (`Confirm`) with its result threaded back as a `CommandEffectResult`; the pure `update` decides the next step from the log; the edge interpreter performs the real stdin read + process/file I/O. Exactly the constitutional shape for a multi-step I/O workflow. |
| VI. Test evidence mandatory | **PASS** | Fail-first golden fixtures for both report blocks; a scripted-stdin interactive test and a non-interactive-refusal test (real fixtures, synthetic stdin disclosed in the test name); write-audit test. |
| VII. Agent & human share one contract | **PASS** | Both agent surfaces updated equivalently: the `fs-gg-sdd-refresh-agents`/`getting-started` skills (and CLAUDE.md/AGENTS.md guidance) gain the `doctor`/`upgrade` verbs; the report is the single source of truth, agents author nothing new. |
| VIII. Observability & safe failure | **PASS** | `doctor.*`/`upgrade.*` diagnostics distinguish user-input refusal (non-interactive w/o `--yes`, exit 1) from step defect (self-update/re-pin/re-seed failed, exit 2); no-scaffold degrades to a clean "nothing to reconcile" (exit 0); an incomplete reconciliation never reports complete (FR-013). |

**Change tier**: **Tier 1 (contracted change)** — two new commands, two new report blocks, a new
effect + edge-interpreter behavior, agent-skill/docs contract. Requires spec, plan, tasks, `.fsi`,
tests, docs, and a migration note for the additive report blocks.

**Gate result**: PASS — one justified complexity entry (the new `Confirm` effect); no unjustified
violations.

## Project Structure

### Documentation (this feature)

```text
specs/053-upgrade-doctor-remediation/
├── plan.md              # This file
├── research.md          # Phase 0 — R1–R12 decisions (resolves the NEEDS CLARIFICATION set)
├── data-model.md        # Phase 1 — new/extended entities
├── quickstart.md        # Phase 1 — validation scenarios A–H
├── contracts/           # Phase 1
│   ├── doctor-command.md           # doctor report shape, read-only invariant, exit 0
│   ├── upgrade-command.md          # upgrade steps, confirm/--yes, exit taxonomy, no-clobber/consumer-only
│   ├── confirm-effect.md           # the new Confirm effect + edge protocol + interactivity threading
│   └── drift-model.md              # the shared pure drift computation (CLI axis + artifact axis + preview)
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/
├── FS.GG.SDD.Artifacts/
│   └── Diagnostics.fs / .fsi             # + doctor.*/upgrade.* diagnostics (behind/missing/refuse/stepFailed/…)
└── FS.GG.SDD.Commands/
    ├── CommandTypes.fs / .fsi            # + Doctor|Upgrade cases; parse/name/stage/nextLifecycleCommand(None);
    │                                     #   + Confirm effect + Confirmed result field; + DoctorSummary/UpgradeSummary;
    │                                     #   + request inputs (AssumeYes, IsInteractive)
    ├── CommandWorkflow/
    │   ├── Drift.fs                       # NEW — pure drift model shared by doctor+upgrade (CLI axis, artifact axis, steps)
    │   ├── HandlersDoctor.fs             # NEW — read-only staged driver (resolve → read → report), no writes
    │   ├── HandlersUpgrade.fs            # NEW — staged driver: resolve drift → per-step Confirm → apply → finalize
    │   ├── Foundation.fs                 # + doctorReadEffects/upgradeReadEffects; plan/dispatch wiring; reuse initEffects/SeededSkills
    │   └── SeededSkills.fs               # reused as the expected-artifact source of truth (no change, or expose the set)
    ├── CommandWorkflow.fs                # + Doctor|Upgrade branches in nextLifecycleEffects (own drivers, like Scaffold)
    ├── CommandEffects.fs                 # + Confirm edge interpreter (stdin read when interactive; DryRun/non-interactive rules)
    ├── CommandReports.fs                 # + doctor/upgrade outcome+nextAction; extend providerDefectIds→exit 2 for upgrade.*
    ├── CommandSerialization.fs           # + doctor/upgrade json blocks (byte-deterministic)
    ├── CommandRendering.fs               # + doctor/upgrade text lines (rich derives automatically)
    └── CommandHelp.fs                    # + doctor/upgrade top-level entries + per-command flags (--yes, --root)
src/FS.GG.SDD.Cli/
├── Program.fs                            # parse --yes; detect input interactivity; thread into request; Confirm loop tolerated by interpretUntilIdle
└── Rendering.fs                          # detectCapabilities gains input-interactivity (Console.IsInputRedirected) for the confirm gate

tests/
├── FS.GG.SDD.Commands.Tests/            # DoctorCommandTests / UpgradeCommandTests goldens; drift-model unit tests; write-audit
├── FS.GG.SDD.Cli.Tests/                 # three-projection parity; scripted-stdin interactive confirm; non-interactive refusal exit 1
└── fixtures/                            # behind / coherent / no-min / no-provenance scaffolds (+ author-edited artifact case)

docs/
├── release/migrations/                  # NEW note — additive doctor/upgrade report blocks
└── reference/…                          # doctor/upgrade usage; refresh-agents & getting-started skill guidance (Claude ⇔ Codex aligned)
```

**Structure Decision**: The existing multi-project layout is retained. Two **new** handler modules
(`HandlersDoctor.fs`, `HandlersUpgrade.fs`) and one **new** pure module (`Drift.fs`) join the
`CommandWorkflow/` folder beside the existing per-command handlers; everything else is additive edits
to existing files, following the established scaffold/refresh precedent (own staged driver, re-derive
phase from the interpreted-effect log, no new model state field beyond what the report needs).

## Phase 0 — Research

Complete. All spec-open planning choices and the flagged scope forks are resolved (with recommended
resolutions and the two items routed to `/speckit-clarify`) in [research.md](./research.md):
R1 (two peer cross-cutting commands, own drivers), R2 (declarative-truth minimum reading, reuse 052),
R3 (expected-artifact set = `SeededSkills` × 2 surfaces + early-stage guidance), R4 (self-update ↔
re-seed binary identity — **clarify**), R5 (diff-rendering fidelity — **clarify/confirm**), R6
(template re-pin scope — **clarify**), R7 (`Confirm` effect + interactivity threading), R8
(no-clobber re-seed reuses `init` effects), R9 (consumer-only re-pin write path + write kind/policy),
R10 (exit-code taxonomy + `upgrade.*` defect ids → exit 2), R11 (three-projection additive report
blocks), R12 (no-scaffold / no-minimum degradation).

## Phase 1 — Design & Contracts

Complete. Artifacts generated: [data-model.md](./data-model.md), the four [contracts/](./contracts/)
specs, [quickstart.md](./quickstart.md). Agent context (`CLAUDE.md` SPECKIT block) updated to point
at this plan.

## Phase 2 — Task planning approach (for `/speckit-tasks`, not executed here)

Expected task ordering (spec → fsi → tests → impl, bottom-up by dependency):

1. **Command plumbing**: add `Doctor`/`Upgrade` to `SddCommand` with `parseCommand`/`commandName`/
   `commandStage`/`nextLifecycleCommand (None)`; help entries. Baseline refresh.
2. **`Confirm` effect + result**: extend `CommandEffect`, `CommandEffectResult` (`.fsi` first),
   the edge interpreter (`interpret`), and interactivity threading into `CommandRequest`
   (`AssumeYes`, `IsInteractive`) + `Program.fs`/`Rendering.fs`. Fail-first edge tests.
3. **Diagnostics**: `doctor.*`/`upgrade.*` codes; extend the exit-2 defect-id set with `upgrade.*`
   step-defect ids.
4. **`Drift.fs`** (pure): CLI-axis (reuse `Fsgg.Version` + 052 minimum reading), artifact-axis
   (expected = `SeededSkills.skillNames` × `.claude`/`.codex` + `.fsgg/early-stage-guidance.md`),
   previewed steps. Unit-tested against behind/coherent/no-min/no-provenance inputs.
5. **`HandlersDoctor.fs`** + `DoctorSummary`: read effects → pure drift → report; zero writes.
6. **`HandlersUpgrade.fs`** + `UpgradeSummary`: staged driver — resolve drift → per-step `Confirm`
   → apply (`RunProcess` self-update / no-clobber re-seed `WriteFile` / consumer-only re-pin
   `WriteFile`) → finalize with applied/skipped/failed accounting; `--yes` and non-interactive
   refusal paths.
7. **Report projections**: `nextLifecycleEffects` branches; json + text emit; nextAction; verify
   rich derives automatically; three-projection + exit-code parity tests; write-audit test.
8. **Docs**: migration note; doctor/upgrade reference + refresh-agents/getting-started skill guidance
   (Claude ⇔ Codex aligned), noting `upgrade` is the *only* mutating remediation verb and CI keeps
   pinning via `.config/dotnet-tools.json`.

Cross-repo: the template re-pin scope (R6) and the self-update package-id/tool-manifest details
(R4) touch epic-#85 (FS-GG/.github#85) and the Templates half — confirm via
`cross-repo-coordination` before merge; this feature adds **no** new versioned cross-repo contract
surface (spec Assumption "No registry surface change").

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New `Confirm` effect + a `Confirmed` result field + edge-interpreter stdin read | ADR-0009 requires per-step diff **and confirmation** before any mutation; the CLI is otherwise a non-interactive batch pipeline with no stdin path. Modelling confirmation as an `Effect` keeps the MVU boundary (Principle V) and lets the pure `update` re-derive step staging from the interpreted-effect log. | Prompting ad-hoc inside the edge/`Program.fs` outside the effect model was rejected: it would hide I/O and a state transition from `update`, break the "pure transition + edge interpreter" contract, and make the interactive loop untestable through the standard model harness. |
