# Quickstart: Validating Feature 046

How to prove "documented authoring contracts + self-correcting diagnostics" works end to end.
Run from the repo root.

## Prerequisites

- .NET SDK (`net10.0`).
- A clean working tree on `046-document-authoring-contracts`.

## Build & test

```sh
dotnet build
dotnet test tests/FS.GG.SDD.Commands.Tests
```

Expected: green, including the new `AuthoringDocsContractTests` (the docs-vs-behavior drift
guard, SC-005) and the regenerated golden fixtures for the three enriched diagnostics.

## Scenario A — coverage diagnostic shows the exact form (US2 / FR-007)

1. In a work item, write a requirement as a bold id only (`**FR-001** …`) with no `- FR-001:`
   coverage line.
2. Run `fsgg-sdd checklist --work <id>`.
3. **Expect**: the missing-coverage review names `FR-001` and its correction shows the exact form
   to write — `- FR-001: <text> (covers AC-###)` — and calls out that the bold form is not
   recognized. Following it verbatim and re-running `checklist` clears the review.

## Scenario B — evidence correction states the satisfaction rule (US2 / FR-008)

1. Reach the `evidence` stage; leave an obligation unsatisfied (or declare it `synthetic: true`
   with `result: pass`).
2. Run `fsgg-sdd evidence --work <id>` (and `verify`).
3. **Expect**: the correction states that a satisfying declaration must be **non-synthetic** with
   `result: pass` (a synthetic pass does not satisfy), or an accepted deferral.

## Scenario C — intent correction shows the labeled form (US2 / FR-009)

1. Run `fsgg-sdd specify --work <id> --input "just some prose with no labels"`.
2. **Expect**: `missingSpecificationIntent` — the **message** names which facts are missing
   (scope, measurable requirement), and the **correction** now shows the labeled-line form
   (`value: …` / `scope: …` / `requirement: …`).

## Scenario D — docs are co-verified and self-sufficient (US1 / SC-001..SC-002)

1. Open `docs/reference/authoring-contracts.md`. Confirm it documents the accepted/rejected
   coverage forms, the `evidence.yml` `kind`/`result` vocabulary, the satisfaction rule, and the
   `evidence.yml` disambiguation note.
2. Copy the accepted coverage example and the satisfying `evidence.yml` example into a fresh work
   item and run the lifecycle through `verify` — they pass on the first attempt with no need to
   inspect CLI internals.
3. The drift guard (`dotnet test`) proves every documented accepted form is accepted and every
   rejected form rejected by the live parsers.

## Scenario E — contract invariants hold (FR-010 / SC-004 / SC-006)

- `git diff` the regenerated goldens: the only JSON changes are the three `correction` strings —
  no field added/removed/renamed, no code/severity/exit/outcome change.
- Grep the diff for any external provider package id, template id, path, or docs URL, and for any
  parser-grammar change: there must be none (FR-012 / SC-006).
- Confirm `.claude/skills/fs-gg-sdd-project/SKILL.md` and `.codex/skills/fs-gg-sdd-project/SKILL.md`
  carry the same new authoring guidance (FR-013).
