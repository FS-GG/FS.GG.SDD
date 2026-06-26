# Contract: lifecycle-artifacts module decomposition

The "interface contract" for this CLI/library refactor is the **public F#
surface** of `FS.GG.SDD.Artifacts`. R3 preserves that surface but redistributes
it from one `module LifecycleArtifacts` into per-family `[<AutoOpen>]` modules.

## Invariant

The set of public types and `val`s is **unchanged in aggregate**. The old
`LifecycleArtifacts.fsi` (722 lines) equals, member-for-member, the **union** of
the new per-family `.fsi` files. No `val` signature changes; no record field or
DU case changes.

What changes (allowed by the planning relaxations):

- The qualifying module name. `LifecycleArtifacts.parsePlanFacts` becomes
  `parsePlanFacts` (via `open FS.GG.SDD.Artifacts`) or `Plan.parsePlanFacts`.
- Consumer `open`/qualifier lines (mechanical, compiler-guided).

## Public-surface checklist (must all remain present, same signatures)

Parsers (entrypoints consumed across the repo):

- `standardArtifactContracts : unit -> LifecycleArtifactContract list`
- `parseProjectConfig`, `parseSddLifecyclePolicy`, `parseAgentGuidanceConfig`
- `parseWorkItemMetadata`
- `specificationStandardSections`, `parseSpecificationFacts`
- `clarificationStandardSections`, `parseClarificationFacts`
- `checklistStandardSections`, `parseChecklistFacts`
- `planStandardSections`, `parsePlanFacts`
- `parseRequirements`, `parseDecisions`
- `parseTaskFacts`, `parseTasks`
- `parseAnalysisView`
- `parseEvidenceArtifact`, `parseEvidence`
- `parseVerificationView`
- `parseShipView`
- `parseGeneratedAgentGuidance`
- `loadWorkItemFromSnapshots`

(Signatures are copied verbatim from the current `LifecycleArtifacts.fsi`
lines 699–722 into the owning family `.fsi`.)

Types: every record and DU listed in [data-model.md](../data-model.md) keeps its
exact definition (fields, order, cases) in its new home module.

## Acceptance (how the contract is enforced)

The **existing test suite is the contract enforcer** (stakeholder decision: tests
are the only behavioral gate). A passing `dotnet build` + `dotnet test` proves:

1. Every public type/`val` is still reachable (tests reference them).
2. Record construction and DU-case usage resolve through `open FS.GG.SDD.Artifacts`.
3. Parsing/serialization behavior is preserved to the degree the tests assert.

Supplementary (non-gating) checks documented in
[quickstart.md](../quickstart.md): largest-file line count (FR-009), per-family
file presence (FR-001), warning-relocation sanity (FR-008).
