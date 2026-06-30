# Contract: Generated verification-obligation `requiredSkills`

**Owner**: FS.GG.SDD task generator (`plannedTasks` → `obligationTasks`).
**Surface**: `requiredSkills` on every generated verification-obligation task in
`readiness/<id>/work-model.json` and the tasks artifact.

## Resolution

Let `declared = ProjectLifecycleConfig.TestFramework` (from `.fsgg/project.yml`).

```text
testSkill =
    match declared with
    | Some raw when raw is non-blank -> normalize raw     # trim, invariant-culture lowercase, slugify whitespace
    | _                              -> "automated-tests" # framework-neutral

verification-obligation task requiredSkills = sort (distinct [ testSkill; "readiness-evidence" ])
```

## Guarantees

| ID | Guarantee |
|----|-----------|
| FR-001/FR-004 | The test skill reflects the product's framework; it is **never** `xunit` unless the author explicitly declares `xunit`. |
| FR-002/SC-003 | Declared framework ⇒ token derived from that declaration (e.g. `expecto`). |
| FR-003/SC-002 | Undeclared/blank ⇒ exactly the neutral token `automated-tests`; no framework-specific token appears in generated task metadata. |
| FR-005/SC-004 | Only the verification-obligation test skill changes. `readiness-evidence` and all other categories' `requiredSkills` are unchanged. |
| FR-006/SC-005 | Output is deterministic: `resolveTestSkill` is pure; skills are sorted+distinct; re-running on identical inputs is byte-identical. |
| FR-008 | `evidence.missingRequiredSkill` is keyed to the resolved token (no verify code change), so author evidence covering the obligation satisfies it. |
| FR-009 | Observable across `--json`/default, `--text`, `--rich` with no JSON byte change beyond the skill value. |

## Example outputs

Declared `testFramework: expecto`:

```json
{ "id": "T...", "requiredSkills": ["expecto", "readiness-evidence"] }
```

No declaration:

```json
{ "id": "T...", "requiredSkills": ["automated-tests", "readiness-evidence"] }
```

## Non-goals

- No change to the other six task categories' skills.
- No new public API symbol (`resolveTestSkill` lives in the internal
  `ParsingTasks` module).
- No framework detection/validation; no closed framework list.
