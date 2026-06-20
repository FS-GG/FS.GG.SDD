# Contract: Generated Agent Guidance View

Owner: `FS.GG.SDD` (artifact-model library). Status: new in feature
`014-agent-guidance`. Schema version: 1 (diagnose-only migration posture).

## Purpose

Define the generated per-target agent-guidance view that derives Claude and Codex
command/skill guidance from one selected work item's normalized work model. The
structured manifest is the machine contract; the Markdown files are its
projection.

## Artifacts

For each configured target `<target>` (e.g. `claude`, `codex`) under that
target's configured `generatedRoot` (default
`readiness/<id>/agent-commands/<target>/`):

- `guidance.json` — the structured `GeneratedAgentGuidance` manifest (machine
  contract).
- `commands.md`, `skills.md` — Markdown projections rendered from the manifest
  (agent-facing surface), each marked as generated with a source reference.

The view kind is the existing `GeneratedViewKind.AgentCommands`.

## `GeneratedAgentGuidance` (manifest) shape

Public `.fsi` additions in `LifecycleArtifacts` (signatures precede
implementation):

```fsharp
type GuidanceCommandEntry =
    { Id: string
      Title: string
      Stage: string
      Purpose: string
      RelatedIds: string list }

type GuidanceSkillEntry =
    { Id: string
      Title: string
      Capability: string
      RelatedIds: string list }

type GeneratedGuidanceFileRef =
    { Path: string
      Kind: string }          // "commands" | "skills"

type GeneratedAgentGuidance =
    { SchemaVersion: SchemaVersion
      ViewVersion: string
      WorkId: WorkId
      TargetId: string
      Generator: string
      Generated: bool                       // always true; the generated marker
      Sources: AnalysisSourceRecord list    // work model (path, digest, schema, status)
      BehaviorModelDigest: SourceDigest      // digest of the normalized behavior model
      Commands: GuidanceCommandEntry list
      Skills: GuidanceSkillEntry list
      RenderedFiles: GeneratedGuidanceFileRef list
      Diagnostics: Diagnostic list }

val parseGeneratedAgentGuidance:
    snapshot: FileSnapshot -> Result<GeneratedAgentGuidance, Diagnostic list>
```

## Derivation rules

- The manifest is derived **only** from `readiness/<id>/work-model.json`. The
  hand-owned `GuidancePath` files (`CLAUDE.md`/`AGENTS.md`) are never read as
  derivation input.
- `Commands` and `Skills` are derived from lifecycle stages, requirements,
  decisions, tasks, and evidence obligations in the work model and sorted by
  stable id.
- `BehaviorModelDigest` is the digest of the normalized behavior model
  (`Commands` + `Skills` + work-id + lifecycle facts, excluding presentation-only
  fields). All targets generated in one run share the same behavior model, so
  their `BehaviorModelDigest` values are identical by construction.
- `Sources` records the work-model path, digest, schema version, and schema
  status using the existing `AnalysisSourceRecord`.

## Currency

- Currency reuses `GenerationManifest`/`isStale` over `Sources`. A target's
  guidance is `current` only when its recorded work-model digest, schema version,
  and generator identity match the current work model.
- Classify each target view as `current`, `missing`, `stale`, `malformed`, or
  `blocked` (`GeneratedViewCurrency`), reported via the existing
  `GeneratedViewState`.
- A missing, stale, malformed, or blocked work model blocks generation for all
  targets; no manifest is derived from an unusable model.

## Determinism

- Targets sorted by `TargetId`; `Commands`/`Skills`/`RenderedFiles`/`Sources`
  sorted by stable id/path.
- No wall-clock timestamps, durations, absolute host paths, terminal width, ANSI
  styling, directory enumeration order, host path separators, or random values
  appear in the manifest or Markdown.
- Identical project state and input yield byte-identical manifests and Markdown.

## Diagnostics

Stable agent-guidance diagnostic ids (each with affected artifact/target,
severity, explanation, correction) cover at least: malformed manifest schema
version, malformed manifest body, stale generated guidance vs work model,
behavior-model digest mismatch (Claude/Codex divergence), and unknown source
reference. Diagnostics follow the existing `Diagnostic` shape.
