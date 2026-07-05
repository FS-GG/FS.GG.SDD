# Quickstart / Validation: obligation ref preservation + `--from-tests`

Proves the feature end-to-end against a real work item whose task graph contains a plan-decision
task. See [contracts/evidence-scaffolding-refs.md](./contracts/evidence-scaffolding-refs.md) for
the ref-routing table and [data-model.md](./data-model.md) for the field-level changes.

## Prerequisites

- Repo builds: `dotnet build`
- A lifecycle work item taken through `plan` with at least one `PD-###` plan decision (whose plan
  text references an `FR-###`), then `tasks` (so the graph includes an
  `Implement plan decision PD-###` task with that PD id and FR in its `SourceIds`).

## Scenario A — plan-decision obligation preserves PD (and recovers FR)

```sh
fsgg-sdd evidence --work <id>            # scaffold obligations
```

**Expected**: in `work/<id>/evidence.yml`, the obligation for the plan-decision task shows
`planDecisionRefs: [PD-###]` and (when the plan decision traced an FR) `requirementRefs: [FR-###]`
— **without** consulting `tasks.yml` to learn the origin (SC-001, SC-002, SC-003).

## Scenario B — requirement obligation unchanged (no regression)

**Expected**: the obligation for an `Implement requirement FR-###` task still shows
`requirementRefs: [FR-###]` (US1 scenario 2 — must not regress).

## Scenario C — clarification decision routed correctly

For a task whose lineage carries a `DEC-###`, **expected**: `clarificationDecisionRefs: [DEC-###]`
and `planDecisionRefs: []` — never misfiled as a plan decision (US1 scenario 4).

## Scenario D — determinism / idempotency

```sh
fsgg-sdd evidence --work <id> --json > a.json
fsgg-sdd evidence --work <id> --json > b.json
diff a.json b.json                        # expect: no differences (SC-005)
```

## Scenario E — `--from-tests`

```sh
fsgg-sdd evidence --work <id> --from-tests tests/FS.GG.SDD.Foo.Tests
```

**Expected**: each newly scaffolded obligation carries a verification-kind source referencing that
path. Re-run **without** the flag ⇒ output byte-identical to the prior release aside from the
populated refs (SC-004).

```sh
fsgg-sdd evidence --work <id> --from-tests "   "
```

**Expected**: blank value treated as absent — no source seeded (FR-009). A non-existent but
non-blank path is recorded as a declared verification source; the verify stage later flags it as
missing (evidence declares, verify validates).

## Regression guard

- `--rich` / `--text` projections render the same facts as `--json`; JSON bytes unchanged beyond
  the populated ref fields (FR-010).
- Golden fixtures for scaffolded `evidence.yml` updated to show populated refs and reviewed.
