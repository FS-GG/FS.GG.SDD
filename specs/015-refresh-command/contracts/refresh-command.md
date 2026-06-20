# Contract: `fsgg-sdd refresh` Command

**Status**: Tier 1 contract (new command) | **Command**: `refresh` |
**Lifecycle position**: cross-cutting (`nextLifecycleCommand Refresh = None`)

`refresh` brings one selected work item's SDD-owned generated views back into
currency from its current declared sources, renders the human-readable
`summary.md`, and emits one deterministic report. It is not a lifecycle authoring
stage and authors no source artifact.

## Invocation

```
fsgg-sdd refresh --work <id> [--json|--text] [--dry-run]
```

- `--work <id>` (required): the single work item to refresh (FR-002, FR-015).
- `--json` / `--text` (default `--text` per existing CLI convention): output
  format. JSON is the authoritative machine report; text is a projection of it
  (FR-019).
- `--dry-run`: report proposed generated-view changes without modifying any
  authored or generated file (FR-021).

Maps to `CommandRequest { Command = Refresh; WorkId = Some id; DryRun = ...;
OutputFormat = ...; OverwritePolicy = AllowGeneratedRefresh; GeneratorVersion =
... }`.

## Behavior (MVU workflow)

1. **LoadProject** — require an initialized SDD project; else `outsideProject`
   and outcome `Blocked` (FR-002).
2. **LoadWorkItem** — validate one work id against the normalized work model;
   empty/malformed/mismatched/duplicate → block (FR-015).
3. **PlanGeneratedViewRefresh** — for each SDD-owned view in source-of order
   (work model → analysis → verify → ship → agent guidance → summary):
   - evaluate currency from recorded `SourceIdentity` digests vs. current
     sources via `GenerationManifest.isStale` and schema status;
   - if a declared source is missing/malformed/stale → view `Blocked`, emit the
     matching diagnostic, do not fabricate (FR-009, FR-010);
   - if the existing generated view file is malformed → regenerate from sources
     (FR-009);
   - if an upstream view could not reach `Current` → dependent view `Blocked`,
     naming the upstream (FR-011);
   - otherwise refresh via the existing per-view generator (FR-003, FR-004);
   - `agent-commands` with no agent config / no targets → `NotApplicable` (no
     diagnostic).
4. Render `summary.md` last from the structured readiness data; if its inputs are
   unusable, emit `refreshUnrenderableSummary` and mark summary `Blocked`
   (US3-3).
5. **EffectInterpreted** — under normal run, write only generated views under
   their generated roots (`WriteFile ... GeneratedView` / `... AgentGuidance`);
   under `--dry-run`, emit no mutations.
6. **BuildReport** — assemble the report, disposition, and next action.

Refresh always refreshes the views that *can* be refreshed even when others are
blocked (US2-2).

## Outcome and disposition mapping (FR-014, FR-016)

| Condition | `Outcome` | `RefreshDisposition` | Next action |
|---|---|---|---|
| All applicable views `Current` after refresh | `Succeeded` | `refreshed-current` | continue lifecycle / rely on refreshed readiness |
| Some views refreshed/current, ≥1 blocked | `SucceededWithWarnings` | `partially-blocked` | correct the named source/upstream view, or re-run the responsible lifecycle command |
| Invalid project/id, or no view refreshable | `Blocked` | `blocked` | correct project context / work id / sources |
| Nothing to do (all already current, no writes) | `NoChange` | `refreshed-current` | rely on refreshed readiness |

Exit code via existing `exitCodeForReport` (non-zero when blocking diagnostics
present).

## Guarantees

- **Authored sources preserved** (FR-012, FR-013): authored lifecycle artifacts,
  `.fsgg/*.yml`, `CLAUDE.md`/`AGENTS.md` are never created/updated/reordered/
  normalized/removed; they appear as `Preserve`/`NoChange` in `ChangedArtifacts`.
- **Determinism** (FR-018, SC-004): identical state + input → byte-identical
  refreshed views and JSON report.
- **Governance independence** (FR-022–FR-024, SC-009): runs without Governance;
  optional Governance pointers surface as advisory `GovernanceCompatibility`
  facts only; no freshness/route/profile/gate/audit/release/boundary enforcement,
  including no stale-view blocking at a protected boundary.
