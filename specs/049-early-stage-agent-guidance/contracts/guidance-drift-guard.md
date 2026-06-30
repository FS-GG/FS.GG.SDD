# Contract: early-stage guidance drift-guard (zero dangling references)

**Test**: `tests/FS.GG.SDD.Commands.Tests/EarlyStageGuidanceContractTests.fs`,
modeled on feature 046's `AuthoringDocsContractTests`. Enforces FR-007 / SC-003 —
*every* command, path, heading, and stable-id format the static guidance names
resolves to something that genuinely exists in the live SDD contract, so the prose can
never silently drift from the parser.

## Assertions (each over the live source, not a copy)

1. **Commands**: every `fsgg-sdd <stage>` named for the four pre-work-model stages is
   a real lifecycle stage in `Identifiers.allStages` and the stage ordering
   `charter → specify → clarify → checklist` matches `nextLifecycleCommand`
   (`CommandTypes.fs:541-556`).
2. **Headings**: the heading list the guidance gives for each stage **equals** that
   stage's live standard-section list — Charter (`ParsingEarly.fs:288-313`), Spec
   (`Specification.specificationStandardSections`), Clarify
   (`Clarification.clarificationStandardSections`), Checklist
   (`Checklist.checklistStandardSections`). No heading named that is absent from the
   live list; (recommended) no live required heading omitted.
3. **Stable ids**: every id prefix the guidance names (`FR`, `US`, `AC`, `SB`, `AMB`,
   `CQ`, `DEC`, `CHK`, `CR`) is a real `Identifiers` scoped-id prefix and the
   `^PREFIX-\d{3,}$` shape is stated correctly.
4. **Paths**: every path the guidance references exists or is produced by the
   lifecycle — `.fsgg/early-stage-guidance.md` itself after `init`,
   `docs/reference/authoring-contracts.md`, and the
   `readiness/<id>/agent-commands/<target>/` generated-view location.
5. **Authoring contracts**: the §1.1 coverage-line rule and §1.2 evidence
   satisfaction rule the guidance states are consistent with
   `docs/reference/authoring-contracts.md` (the same drift-guard discipline feature
   046 applies to that page via the live parser).

## Determinism cross-check

- Two `init` runs over a clean dir produce byte-identical `.fsgg/early-stage-guidance.md`.
- Two `agents` (and two `refresh`) runs over an early-only fixture produce
  byte-identical reports (the existing generate-twice convention,
  `AgentsCommandTests.fs:186-197`).

A failure here is a build failure — the guidance cannot ship with a dangling
reference, reproducing the original failure mode this feature exists to remove.
