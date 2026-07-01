# Contract: `fsgg-sdd doctor`

Feature `053-upgrade-doctor-remediation` · reads FR-001–FR-005, FR-014–FR-016, SC-001, SC-007.

## Identity

- Cross-cutting command; `nextLifecycleCommand Doctor = None`; reachable only via the CLI, never
  from a lifecycle command path (FR-001).
- Operates on `--root` (default `.`), like `scaffold`. No `--work`.

## Read-only invariant (FR-002 / SC-001)

`doctor` MUST make **zero** writes to the CLI installation or any consumer artifact. It plans only
read effects (`ReadFile`/`EnumerateDirectory`) — never `WriteFile`/`RunProcess`/`SetExecutable`/
`Confirm`. The working tree MUST be byte-identical before and after. A write-audit test asserts no
mutating effect is ever emitted on any doctor path.

## Inputs read (declarative truth, R2/R3)

1. `.fsgg/scaffold-provenance.json` → `ProviderName`, recorded `RequiredMinimumCliVersion`.
2. `.fsgg/providers.yml` → live provider descriptor `MinimumCliVersion` (nested
   `minimumFsggSdd.version`).
3. Installed CLI = `request.GeneratorVersion.Version`.
4. Presence of each expected seeded artifact: `.claude/skills/<name>/SKILL.md` and
   `.codex/skills/<name>/SKILL.md` for every `SeededSkills.skillNames`, plus
   `.fsgg/early-stage-guidance.md`.

## Report (`DoctorSummary`, data-model E4)

- **CLI axis**: `behind` / `atOrAbove` / `coherentByAbsence` (no declared minimum, FR-016) /
  `undeterminable` (installed version unparseable, R12); with the behind-by delta when behind
  (FR-003).
- **Artifact axis**: expected count + sorted `MissingArtifactPaths` (FR-004).
- **Preview**: `PreviewSteps` — a dry-run of what `upgrade` would change across the three steps,
  applying **none** of it (FR-005). Re-pin previews as `noTarget` unless a value-agnostic template
  drift signal exists (R6).
- **Coherent case**: CLI at/above-or-absent AND no missing artifacts AND no re-pin target →
  `IsCoherent = true`, "coherent — nothing to reconcile" (US1-AC3).
- **No provenance**: `HasProvenance = false` → "no scaffold provenance — nothing to reconcile"
  (FR-015).

## Outcome & exit code

- Always exit **0** whenever it produces a report, including with drift present (FR-002).
- Outcome is `NoChange` (coherent / no provenance) or `SucceededWithWarnings` (drift present);
  never `Blocked`.

## Projections (FR-014 / SC-007)

- Same `CommandReport` projected json (default) / text / rich, precedence `--rich > --text > --json`.
- Rich adds/drops no fact and changes no json byte; degrades to zero-ANSI when non-interactive/
  redirected or `NO_COLOR`/`TERM=dumb`.

## Acceptance mapping

| Scenario | Assertion |
|----------|-----------|
| US1-AC1 | Behind scaffold: installed, required minimum, behind-by delta, missing skills named. |
| US1-AC2 | Any drift: preview emitted, zero writes, exit 0 (working tree byte-identical). |
| US1-AC3 | Coherent scaffold: "nothing to reconcile", exit 0. |
| US1-AC4 | json/text/rich carry identical facts; rich changes no json byte. |
