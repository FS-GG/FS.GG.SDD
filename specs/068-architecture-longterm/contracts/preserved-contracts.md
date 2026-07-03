# Preserved Contracts (Tier-2 guardrail): feature 068

This feature is a **pure internal refactor / hardening**. Its defining contract is
the list of things that MUST NOT change. This document is the checklist the
implementation and `/speckit-analyze` verify against.

## MUST NOT change (byte-for-byte)

| Contract surface | How verified |
|---|---|
| `analysis.json` / `verify.json` / `ship.json` emitted bytes | Regenerate for representative work items pre/post; `git diff` of the readiness golden fixtures is empty (FR-002) |
| JSON automation contract for **every** command (default/`--json`) | Golden/deterministic JSON fixtures unchanged; diff empty (FR-010) |
| Exit codes (0/1/2 taxonomy) and stream routing (stdout/stderr) | Existing CLI/process tests stay green |
| `--text` / `--rich` projections | Existing rendering tests stay green |
| Persisted schemas (`scaffold-provenance`, work-model, readiness views, registry) & schema versions | No schema/version literal touched; fixture diff empty |
| Public `.fsi` surface of every library | `git diff -- 'src/**/*.fsi'` empty; PublicSurface baselines unchanged |
| Committed surface baselines (`**/*.baseline`) | `git diff -- '**/*.baseline'` empty |
| Agent-skill contract (seeded `fs-gg-sdd-*` set, roots, mirror) | `SeededSkillsTests` drift guard stays green (only its *accessor* is re-pointed, not its pinned set) |
| Generated-view layout / artifact paths | Unchanged; existing view tests green |
| The `validation-report` schema/verdict | Untouched (this feature does not enter the validation harness) |

## MAY change (internal only)

- `src/FS.GG.SDD.Commands/CommandWorkflow/*.fs` internals: module attributes
  (`[<AutoOpen>]` removal), module/file names (Parsing renames), new internal DUs
  and their `toToken` projections, the `writeReadinessEnvelope` /
  `writeGovernanceReadinessTail` extraction. None appear in any `.fsi`.
- `Drift.Step.Outcome` field type (`string` → `UpgradeStepOutcome`) — internal
  record, no `.fsi`.
- `SeededSkills.seededSkills` accessor shape (value → lazy/function) — internal.
- `Foundation.projectIdFromRoot` root-resolution site — internal, output-preserving.
- A code comment on `RegistryDocument.load` documenting the intentional IO edge.
- `AGENTS.md` content — reconciled to equal `CLAUDE.md` (this is a deliberate
  doc change, and the *only* intended content change in the feature; it adds no
  contract).

## Net new (additive, non-contract)

- One new test: the `CLAUDE.md == AGENTS.md` byte-identity guard.
- No new persisted artifact, no new schema, no new CLI surface, no new `.fsi`.

## The one-line gate

> `git diff --stat -- '**/*.baseline' 'src/**/*.fsi' <readiness-golden-dirs> <json-golden-dirs>` is **empty** after the feature, and `dotnet test FS.GG.SDD.sln` is fully green with no new warnings.
