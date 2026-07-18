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

Expected substring (repointed to the vendored skills — FS.GG.SDD#539):
`… See the fs-gg-sdd-clarify skill and the grammar under the fs-gg-sdd-authoring-contracts skill (#4-clarify-decision-tag-resolution-used-by-clarify).`

## 3. Coherent with the lint pre-flight (FR-009)

The pointer lives in the `--json` correction (step 2). `--text`/`--rich` are summaries that do not
print per-diagnostic corrections. The human pre-flight `lint` (076) independently cites the same
grammar **section** — though, as of FS.GG.SDD#539, the remediation pointer names the vendored
`fs-gg-sdd-authoring-contracts` skill while lint's pointer still names `docs/reference/…` (a
separately-tracked follow-up):

```sh
# lint carries its OWN 076 grammar pointer to the corresponding section (coherence, not one string):
fsgg-sdd lint work/<id>/clarifications.md --text | grep -F 'clarify-decision-tag-resolution'
```

Expected: `lint` shows its own defect fix whose grammar pointer cites the corresponding
decision-tag-resolution section.

## 4. Follow the pointer — it resolves (US3)

```sh
# the cited skills are vendored in every scaffold (and in this tool repo) under .claude/.codex/.agents:
ls .claude/skills/fs-gg-sdd-clarify/SKILL.md && echo "skill OK"
grep -n '^## 4\. Clarify decision-tag resolution' .claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md && echo "anchor OK"
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
