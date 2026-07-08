# Implementation Plan: Slim the Evidence Declaration Shape (Omit Always-Null Optional Fields)

**Branch**: `091-evidence-obligation-shape` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/091-evidence-obligation-shape/spec.md`

## Summary

One change to the `fsgg-sdd evidence` writer, resolving the schema half of FS.GG.SDD#165:

**When an optional evidence-declaration field is `None`, omit its key instead of writing `null`.**
The five fields are `syntheticDisclosure`, `rationale`, `owner`, `scope`, and
`laterLifecycleVisibility`.

The change is confined to `renderEvidenceDeclaration` in `HandlersEvidence.fs`. The reader is
untouched: `tryScalarNonNullAt` already collapses absent-key and plain-`null` to `None`, and
`parseSyntheticDisclosure` already yields `None` when the mapping is absent. Omission is therefore
a strict subset of the accepted input language — old files still parse, new files parse to the same
model, and `schemaVersion` does not move.

No `.fsi` change, no `CommandReport` field change, no CLI flag, no persisted schema change, no
exit-code or diagnostic change. **Change tier: Tier 1** (artifact-layout change to the authored
`evidence.yml` surface — the file humans edit and downstream stages read).

The terse `--satisfy` authoring form, the third option in FS.GG.SDD#165, is **deliberately not in
this feature**: it touches `src/FS.GG.SDD.Cli/Program.fs`, which FS.GG.SDD#163 has declared in its
ADR-0021 touch-set. Attempting it here would force sequencing where disjoint parallel work is
available.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution default).

**Primary Dependencies**: YamlDotNet (already present, reader side only); no new packages.

**Storage**: `work/<id>/evidence.yml` — an authored, committed YAML artifact written by the
`evidence` command's `WriteFile` effect. Unchanged mechanism; fewer bytes.

**Testing**: xUnit across `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs` (parse
equivalence of verbose vs slim; quoted-`"null"` boundary) and
`tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs` (emitted-text assertions, populated-field
retention, re-run byte-idempotence).

**Target Platform**: Cross-platform CLI.

**Project Type**: Single project — `FS.GG.SDD.Commands` (writer) over `FS.GG.SDD.Artifacts`
(reader/model).

**Performance Goals**: N/A. Rendering is O(declarations); this strictly reduces output size.

**Constraints**: The emitted YAML MUST stay well-formed with no stray blank lines when keys are
omitted (the current template interpolates each optional renderer on its own line, so a `""` return
would leave a blank line — the renderer must be restructured, not just made to return `""`).
Re-run byte-idempotence (issue #161) MUST hold. A quoted `"null"` MUST survive as a quoted string.

**Scale/Scope**: 1 source file (`HandlersEvidence.fs`, ~30 lines changed), 2 test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Followed. No `.fsi` changes are needed
  (`renderEvidenceDeclaration` and its helpers are `internal` module members, not public surface;
  `EvidenceDeclaration` is unchanged). Semantic tests through the public surface
  (`parseEvidenceArtifact` and the `evidence` command) are written before the writer body changes. ✅
- **II. Structured Artifacts Are the Machine Contract**: `evidence.yml` remains the authoritative
  machine contract. This feature changes only its *serialization density*, not its information
  content. Prose and structured data cannot disagree — the parsed model is bit-identical before and
  after (SC-003 asserts this directly). ✅
- **III. Visibility Lives in `.fsi`**: No public surface changes. `Evidence.fsi` is untouched; no
  surface baseline refresh. ✅
- **IV. Idiomatic Simplicity**: A `string option` per field, `List.choose id`, `String.concat "\n"`.
  No new abstraction, no operators, no reflection. Strictly simpler than the current template. ✅
- **V. Elmish/MVU Boundary**: Unchanged. `renderEvidenceDeclaration` is a pure
  `EvidenceDeclaration -> string`; the write remains a `WriteFile` effect resolved at the edge. No
  new state, no new I/O. ✅
- **VI. Test Evidence Is Mandatory**: Every FR gets a test that fails before and passes after. The
  existing #161 idempotence test is adapted, not deleted, and is **strengthened** by a new
  quoted-`"null"` retention assertion that the current test only covers negatively. Real filesystem
  fixtures (`initializedAnalyzedProject`), no mocks. ✅
- **VII. Agent And Human Workflows Share One Contract**: Agents and humans author and read the same
  `evidence.yml`. The `fs-gg-sdd-evidence` skill *does* document four of the five fields (as the
  deferral requirements) and `RequiredFieldContractTests` enforces that it keeps doing so — but this
  feature edits no skill body, and the four stay gate-required and still written when populated. The
  pinned `sha256` in `FS-GG/.github` `registry/skills.yml` is unchanged. No skill↔gate drift. ✅
- **VIII. Observability And Safe Failure**: No diagnostic is added or removed. Two gates read these
  fields and both read the *parsed model*, never key presence: `evidence.undisclosedSyntheticEvidence`
  (FR-009) and `evidence.missingDeferralRationale` (FR-010). Both block **before** the write, so an
  under-specified declaration never reaches the writer. Omitting a line cannot silence either. ✅

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/091-evidence-obligation-shape/
├── plan.md              # This file
├── research.md          # Phase 0: absent-vs-null decision, scope of fields, migration posture
├── data-model.md        # Phase 1: the optional-field rendering rule
├── quickstart.md        # Phase 1: before/after evidence.yml, how to verify
└── spec.md              # Feature specification
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
└── HandlersEvidence.fs          # renderOptionalScalar / renderSyntheticDisclosure /
                                 # renderEvidenceDeclaration — the only source change

src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
├── Evidence.fs                  # reader — UNCHANGED (already absent≡null)
├── Evidence.fsi                 # UNCHANGED (no public surface change)
└── Internal.fs                  # tryChild / isPlainNullScalar — UNCHANGED, relied upon

tests/FS.GG.SDD.Artifacts.Tests/
└── EvidenceArtifactTests.fs     # verbose-vs-slim parse equivalence; quoted-"null" boundary

tests/FS.GG.SDD.Commands.Tests/
├── EvidenceCommandTests.fs      # emitted-text assertions; populated-field retention;
│                                # re-run byte-idempotence (adapted #161 test)
└── goldens/readiness/           # regenerated: digest cascade, see below
    ├── verify.json
    ├── ship.json
    └── summary.md
```

**Digest cascade (discovered during implementation).** `evidence.yml`'s bytes are hashed into
`readiness/<id>/work-model.json`'s source digests, whose own digest is in turn embedded in
`verify.json`, `ship.json`, and the `summary.md` projection. Slimming the file therefore moves five
`sha256` values in the committed readiness goldens. The regeneration
(`FSGG_UPDATE_BASELINE=1 dotnet test --filter ReadinessViewGoldenTests`) changes **only** digest
values — zero non-digest lines — which is the proof that no readiness *semantics* moved. The
feature's `Paths:` touch-set was widened to include `tests/FS.GG.SDD.Commands.Tests/goldens/**` and
`scripts/fsgg-coord overlap` re-checked against the live FS.GG.SDD#163 (still DISJOINT) before those
files were touched, per ADR-0021.

**Structure Decision**: Single project. The writer lives in the Commands layer's internal
`HandlersEvidence` module; the model and reader live in the Artifacts layer and are not touched.
This preserves the existing separation: Artifacts owns *what an evidence declaration is*, Commands
owns *how one is rendered to disk*.

**Declared touch-set (ADR-0021)**, matching `Paths:` on FS.GG.SDD#165 — disjoint from the in-flight
FS.GG.SDD#163 and FS.GG.SDD#174:

```text
src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs
src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fsi
src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs
tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs
tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs
tests/FS.GG.SDD.Commands.Tests/goldens/**      # widened mid-flight (digest cascade)
specs/091-evidence-obligation-shape/**
```

In the event, `Evidence.fs` and `Evidence.fsi` were **not** touched at all — the reader needed no
change, as R1 predicted. They stay in the declared set because the feature reasons about them.

## Design Detail

### The current rendering (the defect)

`renderEvidenceDeclaration` builds one interpolated string in which each optional field occupies a
fixed line:

```fsharp
    synthetic: {if declaration.Synthetic then "true" else "false"}
{renderSyntheticDisclosure declaration.SyntheticDisclosure}
{renderOptionalScalar "rationale" declaration.Rationale}
{renderOptionalScalar "owner" declaration.Owner}
{renderOptionalScalar "scope" declaration.Scope}
{renderOptionalScalar "laterLifecycleVisibility" declaration.LaterLifecycleVisibility}
    notes: {declaration.Notes |> yamlInlineList}
```

with

```fsharp
let renderOptionalScalar name value =
    match value with
    | Some value -> $"    {name}: {yamlString value}"
    | None -> $"    {name}: null"          // <-- the boilerplate
```

Naively returning `""` from the `None` branch does **not** work: each interpolation sits on its own
template line, so an empty return leaves a **blank line** in the emitted YAML. That would satisfy
"no `null` key" while violating FR-004.

### The change

Make the optional renderers return `string option` and splice only the `Some` cases:

```fsharp
let renderOptionalScalar name value =
    value |> Option.map (fun value -> $"    {name}: {yamlString value}")

let renderSyntheticDisclosure (disclosure: SyntheticDisclosure option) =
    disclosure
    |> Option.map (fun disclosure ->
        $"    syntheticDisclosure:\n      standsInFor: {yamlString disclosure.StandsInFor}\n      reason: {yamlString disclosure.Reason}")
```

and in `renderEvidenceDeclaration`, collect them into a block that contributes nothing when empty:

```fsharp
let optionalLines =
    [ renderSyntheticDisclosure declaration.SyntheticDisclosure
      renderOptionalScalar "rationale" declaration.Rationale
      renderOptionalScalar "owner" declaration.Owner
      renderOptionalScalar "scope" declaration.Scope
      renderOptionalScalar "laterLifecycleVisibility" declaration.LaterLifecycleVisibility ]
    |> List.choose id
    |> function
       | [] -> ""
       | lines -> "\n" + String.concat "\n" lines
```

spliced *inline* after `synthetic:` (leading `\n` carried by the block, not by the template) so that
`notes:` follows `synthetic:` directly when every optional is `None`.

Field order among the populated optionals is preserved exactly as today
(`syntheticDisclosure`, `rationale`, `owner`, `scope`, `laterLifecycleVisibility`), which keeps
diffs on populated files minimal and keeps rendering deterministic.

### Why the reader needs no change

| Field | Reader | Absent | Plain `null` / `~` / empty | Quoted `"null"` |
|---|---|---|---|---|
| `rationale`, `owner`, `scope`, `laterLifecycleVisibility` | `tryScalarNonNullAt` | `None` (via `tryChild`) | `None` (via `isPlainNullScalar`) | `Some "null"` |
| `syntheticDisclosure` | `parseSyntheticDisclosure` | `None` (via `tryChild`) | `None` (children absent) | n/a (mapping) |

Absent and plain-`null` are already the same value. Omission removes only inputs the reader maps to
`None` anyway. The quoted-`"null"` column is the feature-161 boundary and is the reason
`isPlainNullScalar` checks `ScalarStyle.Plain`.

### Migration posture

**Schema version: unchanged.** No migration code, no gate, no `--migrate` flag.

The only observable transition is a **one-time normalization diff**: the first `evidence` run after
upgrade rewrites an existing verbose `evidence.yml` without the `null` lines. Subsequent runs are
byte-identical (FR-007). Files written by an older CLI, and files hand-authored with explicit
`null`s, continue to parse (FR-005).

## Verification Plan

| FR | Test | Location |
|---|---|---|
| FR-001, FR-002 | Scaffolded `evidence.yml` contains none of the five keys | `EvidenceCommandTests` |
| FR-003 | Populated `syntheticDisclosure` + 4 scalars survive a re-render verbatim | `EvidenceCommandTests` |
| FR-004 | Emitted text has no blank line / trailing whitespace; re-parses cleanly | `EvidenceCommandTests` |
| FR-005 | Verbose fixture (explicit `null`s) parses; declarations equal the slim fixture's | `EvidenceArtifactTests` |
| FR-006 | Quoted `"null"` parses to `Some "null"` and re-renders quoted | `EvidenceArtifactTests` + `EvidenceCommandTests` |
| FR-007 | Two `evidence` runs produce byte-identical output (adapted #161 test) | `EvidenceCommandTests` |
| FR-008 | `evidence` report/diagnostics/exit code unchanged | existing `EvidenceCommandTests` suite |
| FR-009 | Synthetic-without-disclosure diagnostic still fires with the key omitted | **pre-existing** `evidence blocks undisclosed synthetic evidence without mutation` |
| FR-010 | A populated deferral round-trips all four gate-required fields; an under-specified one blocks with no write | `evidence writes every gate-required field of a deferral declaration`, `evidence blocks an under-specified deferral before the writer can omit its fields` |

**Red-before-green, stated precisely.** Constitution VI requires behaviour-changing code to carry
tests that fail before and pass after. That holds for the tests covering FR-001/002/003/006/007
(they fail against the `origin/main` writer). Three tests are deliberately *not* red-first, and
saying otherwise would be false:

- **T002/T003** are *characterization* tests of the unchanged reader. They must pass **before** the
  change — that is their entire purpose. If they were red, the absent≡null claim would be wrong and
  the feature would need a schema bump.
- **The FR-004 blank-line test** passes on `origin/main` too (the old writer emits `<key>: null`, a
  non-blank line). It is not a regression test for the shipped implementation; it is a guard against
  the plausible-but-wrong `| None -> ""` variant that research.md R4 rejects. Kept for that reason,
  labelled honestly here rather than counted as red-first coverage.
- **FR-009 needs no new test.** The pre-existing `evidence blocks undisclosed synthetic evidence
  without mutation` already feeds an input carrying no `syntheticDisclosure` key at all, so it
  already proves the diagnostic derives from the parsed model. Writing a duplicate would add a green
  test that was never red and prove nothing new.

No mocks: `initializedAnalyzedProject` builds a real on-disk workspace. All seven `quickstart.md`
scenarios were additionally walked against the real `fsgg-sdd` binary, and the emitted `evidence.yml`
compared byte-for-byte against `origin/main`'s writer on identical input (170 → 140 lines; identical
after stripping the 30 `null` lines).

## Agent-facing behavior

Claude and Codex author `evidence.yml` through the same `fsgg-sdd evidence` command and the same
`fs-gg-sdd-evidence` skill. The skill documents the `kind`/`result`/`synthetic` satisfaction rule
and the kind/result vocabularies; it does **not** document `syntheticDisclosure`, `rationale`,
`owner`, `scope`, or `laterLifecycleVisibility`. Its body therefore needs no edit, its pinned
`sha256` in the org `registry/skills.yml` stays valid, and no skill-manifest regeneration is
required.

The user-visible agent benefit is direct: an agent authoring 16–19 obligations reads and writes ~80
fewer lines of nothing.

## Governance integration

None. `evidence.yml` obligation shape is not a registered cross-repo contract — the org registry
(`FS-GG/.github` `registry/`) holds `dependencies.yml`, `repos.yml`, and `skills.yml`, with no
contract row for it. Governance-owned effective-evidence freshness reads the *parsed* model, whose
values are unchanged. No coordination issue, no compatibility-registry entry, no ADR.

## Complexity Tracking

Not required — Constitution Check passed with no violations. The change removes code paths
(`None -> "<key>: null"`) rather than adding them.
