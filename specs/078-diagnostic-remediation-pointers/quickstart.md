# Quickstart: Verify diagnostic remediation pointers

Runnable checks that prove the feature end-to-end. Run from repo root.

## Prerequisites

- .NET 10 SDK; `dotnet build FS.GG.SDD.sln` green.

## 1. Build + full test suite (guards + contracts)

```sh
dotnet test FS.GG.SDD.sln
```

Expected: green, including —
- `RemediationPointersTests` (Commands.Tests): every covered id renders a resolving, non-dangling
  pointer; every cited example path and grammar anchor exists (contract invariants 1–6).
- `ExampleArtifactsContractTests` (Artifacts.Tests): `spec.md` and `plan.md` examples parse with
  zero blocking diagnostics; plus the existing four.
- `CharterExampleContractTests` (Commands.Tests): `charter.md` front matter validates.

## 2. See a pointer in a real blocking run (the canonical TD1 case)

Drive `clarify` into the missing-answer block and confirm the correction now points to the
example + the decision-tag grammar (US1 scenario 1, SC-004):

```sh
# scaffold a throwaway work item with a blocking AMB and no resolving decision, then:
fsgg-sdd clarify --work <id> --json | jq -r '.diagnostics[] | select(.id=="missingClarificationAnswer") | .correction'
```

Expected substring:
`… See the shipped example docs/examples/lifecycle-artifacts/clarifications.md and the grammar at docs/reference/authoring-contracts.md#clarify-decision-tag-resolution.`

## 3. Coherent with the lint pre-flight (FR-009)

The pointer lives in the `--json` correction (step 2). `--text`/`--rich` are summaries that do not
print per-diagnostic corrections. The human pre-flight `lint` (076) independently cites the SAME
grammar anchor:

```sh
# lint carries its OWN 076 grammar pointer to the SAME anchor (coherence, not the same string):
fsgg-sdd lint work/<id>/clarifications.md --text | grep -F 'authoring-contracts.md#clarify-decision-tag-resolution'
```

Expected: `lint` shows its own defect fix whose grammar pointer cites the identical
`authoring-contracts.md` anchor the covered diagnostic's `--json` correction cites.

## 4. Follow the pointer — it resolves (US3)

```sh
test -f docs/examples/lifecycle-artifacts/clarifications.md && echo "example OK"
grep -n '^## Clarify decision-tag resolution' docs/reference/authoring-contracts.md && echo "anchor OK"
```

Expected: both `OK`. (The guard test automates this over the whole covered set.)

## 5. Non-covered corrections unchanged (FR-008 / SC-005)

```sh
# a config/system block keeps its exact prior correction (no example/anchor suffix):
fsgg-sdd charter --work <id> --root /not/an/sdd/project --json \
  | jq -r '.diagnostics[] | select(.id=="outsideProject") | .correction'
```

Expected: `Run fsgg-sdd init or pass --root for an initialized SDD project.` — no pointer suffix.

## Success mapping

| Check | Requirements | Success criteria |
|-------|--------------|------------------|
| 1 (guard) | FR-001, FR-002, FR-006 | SC-001, SC-003 |
| 1 (examples) | FR-004, FR-005 | SC-002 |
| 2 | FR-002 | SC-004 |
| 3 | FR-003, FR-009 | SC-005 |
| 4 | FR-006 | SC-003 |
| 5 | FR-008 | SC-005 |
