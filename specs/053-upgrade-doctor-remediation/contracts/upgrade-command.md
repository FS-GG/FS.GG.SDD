# Contract: `fsgg-sdd upgrade`

Feature `053-upgrade-doctor-remediation` · reads FR-006–FR-013, FR-016, FR-017, SC-002–SC-006.

## Identity

- Cross-cutting command; `nextLifecycleCommand Upgrade = None`; reachable only via the CLI (FR-006).
- Operates on `--root` (default `.`). Flags: `--yes` (explicit non-interactive apply), the standard
  `--json`/`--text`/`--rich`.
- `upgrade` is the **only** command permitted to mutate the CLI installation or consumer artifacts
  for remediation (FR-006/FR-008/SC-003). No other command (scaffold/refresh/agents/lifecycle stage)
  may self-update or re-seed as a side effect — enforced by a write-audit test across commands.

## Reconciliation steps (up to three, FR-007)

| StepId | Applied via | Write target | Diff preview (R5) |
|--------|-------------|--------------|-------------------|
| `cliSelfUpdate` | `RunProcess("dotnet", ["tool"; "update"; …])` at the existing edge | CLI installation (not a consumer file) | `installed X → target ≥Y` |
| `templateRePin` | `WriteFile(".fsgg/providers.yml", …)` **[R6: usually `noTarget`]** | consumer-owned `.fsgg/providers.yml` only (FR-009) | changed-line before/after |
| `artifactReSeed` | replay of `init` seeding effects (no-clobber, R8) | missing seeded skeleton paths only (FR-010) | `+ <path> (new, N bytes)` per file |

Each step is shown as its diff **and** applied only after its own confirmation (interactive) or
under `--yes` (FR-007). Confirmation is modelled as the `Confirm` effect (see
`confirm-effect.md`); the pure staged driver re-derives the next step from the confirmed results in
the interpreted-effect log (like scaffold).

## Ownership & no-clobber invariants (US4)

- **Consumer-only** (FR-009 / SC-005): re-pin rewrites only `.fsgg/providers.yml`; **zero** writes to
  governed registry / provider-descriptor state. A write-audit test asserts the only mutations are
  `.fsgg/providers.yml` and the re-seeded skeleton paths.
- **No-clobber re-seed** (FR-010 / US4-AC2): re-seed materializes only **missing** artifacts via the
  `init` `AgentGuidanceTarget` writes; `canOverwrite` refuses a present, author-edited artifact — it
  is never overwritten.

## Interactivity & the explicit apply flag (FR-011 / FR-012 / US3)

- `--yes` (`AssumeYes = true`): applies the reconciliation without prompting; records
  `Mode = "assumeYes"` (US3-AC1). This non-interactive apply is triggered **only** by `--yes`, never
  implicitly by another command or by non-interactivity alone (FR-011).
- Non-interactive (`IsInteractive = false`) **without** `--yes`: MUST NOT block on a prompt; makes
  **zero** writes and refuses with `upgrade.nonInteractiveNoYes` pointing at `--yes`
  (`Mode = "refusedNonInteractive"`, exit 1) — FR-012 / SC-004.
- CI keeps pinning the tool via `.config/dotnet-tools.json` and relies on the feature-052 fail-closed
  check, not on `upgrade` (US4-AC3).

## Outcome & exit-code taxonomy (FR-013 / SC-006 / R10)

| Situation | Outcome | Exit |
|-----------|---------|------|
| Already coherent (no provenance, or nothing to reconcile) | `NoChange`, `AlreadyCoherent = true` | 0 |
| All confirmed steps applied, no residual drift | `Succeeded` | 0 |
| Some step **declined**/skipped, drift remains | `SucceededWithWarnings`, `ResidualDrift = true`, `upgrade.residualDrift` | 0 |
| A confirmed step **failed** to apply | `Blocked`, `FailedStepIds` non-empty, `upgrade.selfUpdateFailed`/`upgrade.stepFailed` | **2** |
| Non-interactive without `--yes` | `Blocked`, `upgrade.nonInteractiveNoYes` | **1** |

`upgrade` MUST never report an incomplete reconciliation as complete: any failed or declined step
surfaces `ResidualDrift` and the affected step ids (FR-013). A subsequent `doctor` MUST reflect the
residual drift; after a fully successful `upgrade`, a subsequent `doctor` MUST report coherent
(FR-017 / US2-AC2 — subject to R4 for the self-update-in-same-run case).

## No-minimum (FR-016)

When the provider declares no minimum, `upgrade` asserts no CLI version target (the `cliSelfUpdate`
step is `noTarget`); the artifact axis still reconciles.

## Projections (FR-014 / SC-007)

Same `CommandReport` three ways; rich adds/drops no fact and changes no json byte; degrades to
zero-ANSI.

## Acceptance mapping

| Scenario | Assertion |
|----------|-----------|
| US2-AC1 | Interactive confirm-each: self-update + re-pin + re-seed each applied after its own diff+confirm. |
| US2-AC2 | After upgrade, `doctor` reports coherent (per R4 for same-run self-update). |
| US2-AC3 | Already-coherent: no-op, "already coherent", exit 0. |
| US2-AC4 | Declined step: skipped, no write, applied vs skipped distinguished, residual drift surfaced. |
| US2-AC5 | Confirmed step fails: failure + residual drift reported, not "complete", exit 2. |
| US3-AC1 | `--yes` non-interactive: applied without prompting; mode records the explicit path. |
| US3-AC2 | Non-interactive, no `--yes`: zero writes, no hang, refusal → `--yes`. |
| US3-AC3 | Other commands never self-update/re-seed as a side effect. |
| US4-AC1 | Re-pin rewrites only `.fsgg/providers.yml`; no governed state touched. |
| US4-AC2 | Re-seed materializes only missing artifacts; present author-edited ones untouched. |
| US4-AC3 | CI pins via `.config/dotnet-tools.json`; no implicit upgrade. |
