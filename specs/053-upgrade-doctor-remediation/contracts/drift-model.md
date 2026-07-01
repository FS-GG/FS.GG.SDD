# Contract: the shared pure drift model (`Drift.fs`)

Feature `053-upgrade-doctor-remediation` · reads FR-003–FR-005, FR-010, FR-016; SC-001. See research
R2/R3/R5/R6/R12.

## Purpose

A single pure module consumed by **both** `HandlersDoctor` and `HandlersUpgrade`, so the drift a
`doctor` previews and the drift an `upgrade` reconciles are computed by the same code (no second
source of truth). It performs **no I/O**: it consumes snapshots already read via effects and returns
the drift picture + the previewed `ReconciliationStep` list.

## Inputs (all already snapshotted at the edge)

- `provenance: ScaffoldProvenanceRecord option` (from `.fsgg/scaffold-provenance.json`; `tryParse`).
- `descriptor: ProviderDescriptor option` (resolved from `.fsgg/providers.yml` by
  `provenance.ProviderName`, reusing scaffold's `parseProviderRegistry`).
- `installedVersion: string` (`request.GeneratorVersion.Version`).
- `presentArtifacts: Set<string>` (which expected seeded paths exist on disk).

## CLI axis (R2 / FR-003 / FR-016 / R12)

Live descriptor minimum wins over the provenance-recorded one when they disagree (spec Assumption).

| Case | `CliAxis` |
|------|-----------|
| No declared minimum | `coherentByAbsence` (FR-016) — no staleness asserted |
| `Fsgg.Version.compare installed minimum = Some -1` | `behind` (+ behind-by delta) |
| `Some 0` / `Some 1` | `atOrAbove` |
| `None` (installed unparseable) | `undeterminable` (R12) — no false ordering |

Reuses `Fsgg.Version` and the 052 minimum-reading semantics verbatim; embeds no version literal.

## Artifact axis (R3 / FR-004 / FR-010)

Expected set = for every `Internal.SeededSkills.skillNames`: `.claude/skills/<name>/SKILL.md` and
`.codex/skills/<name>/SKILL.md`; plus `.fsgg/early-stage-guidance.md`. `MissingArtifactPaths` =
expected − present, sorted. This is exactly the set `init` seeds, so `upgrade`'s re-seed re-materializes
the missing subset via `initEffects` no-clobber writes (R8) — the model names them, the handler writes
them.

## Previewed steps (R5 / R6 / E6)

Emits a `ReconciliationStep` for each of the three axes:

- `cliSelfUpdate`: `wouldApply` when `CliAxis = behind`; else `noTarget` (incl. `coherentByAbsence`/
  `undeterminable`). `DiffPreview = "installed X → target ≥Y"`. `TargetPaths = []`.
- `templateRePin`: **`noTarget`** unless a value-agnostic template-version drift signal is available
  from the descriptor (R6 — usually inert pending the epic-#85 Templates half). When targeted,
  `TargetPaths = [".fsgg/providers.yml"]` and a changed-line diff preview. SDD embeds no template
  literal either way.
- `artifactReSeed`: `wouldApply` when `MissingArtifactPaths` is non-empty; `DiffPreview` lists
  `+ <path> (new)` per missing file; `TargetPaths = MissingArtifactPaths`.

## Coherence

`IsCoherent` ⇔ `CliAxis ∈ {atOrAbove, coherentByAbsence}` **and** `MissingArtifactPaths = []`
**and** the re-pin step is `noTarget`. Drives doctor's "nothing to reconcile" and upgrade's
"already coherent" no-op.

## No-provenance (FR-015 / R12)

`provenance = None` → the model reports `HasProvenance = false` and no steps; both commands degrade
to "nothing to reconcile", zero writes, exit 0.

## Purity & test posture

- Pure function, unit-tested directly against constructed inputs: behind / atOrAbove /
  coherentByAbsence / undeterminable × (all-present / some-missing) × (provenance / none).
- Determinism: sorted path lists; no clock/absolute-path; identical output feeds doctor's preview and
  upgrade's plan so they never disagree.
