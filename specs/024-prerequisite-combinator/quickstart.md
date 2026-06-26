# Quickstart: validating the prerequisite combinator + handler shell

This is a **behavior-preserving Tier 2 refactor** (roadmap R1). Validation =
proving the existing contract is byte-for-byte intact and the duplication is
gone. There is no new user-facing behavior to demonstrate.

## Prerequisites

- .NET SDK 10.0.301 (per repo baseline)
- Clean working tree on branch `024-prerequisite-combinator`
- Post-R4 baseline: **438** tests green, **0** FS0025 warnings

## 1. Capture the before-baseline (on `main` or pre-change)

```bash
dotnet build -c Release --no-incremental 2>&1 | tee /tmp/before-build.txt
grep -c 'FS0025' /tmp/before-build.txt        # expect 0
grep -c 'FS3261' /tmp/before-build.txt        # record N (nullness baseline)
dotnet test -c Release 2>&1 | tee /tmp/before-test.txt   # expect 438 passed
```

Optionally snapshot deterministic view output for a representative work item
(any existing golden/fixture the suite already drives) to diff later.

## 2. Apply the refactor

Edit only `src/FS.GG.SDD.Commands/CommandWorkflow.fs`:
1. Add `PrerequisiteResolution` + `resolvePrerequisites` (after the
   `*Prerequisite*` helpers, before `computeCharterPlan`) — see
   `contracts/prerequisite-resolver.md`.
2. Add `runHandler` — see `contracts/run-handler.md`.
3. Rewrite the twelve `compute*Plan` handlers to consume the resolver prefix and
   wrap their tail in `runHandler`.

Do **not** touch `CommandWorkflow.fsi`, any `*.fsi`, the surface baseline, or any
test source (beyond mechanical updates if an internal helper is renamed — there
should be none, since handlers are not in the `.fsi`).

## 3. Validate (the binding gate)

```bash
dotnet build -c Release --no-incremental 2>&1 | tee /tmp/after-build.txt
```

| Check | Expectation | Spec ref |
|---|---|---|
| Build | green | — |
| `grep -c FS0025 /tmp/after-build.txt` | **0** (unchanged) | SC-005 |
| `grep -c FS3261 /tmp/after-build.txt` | **= N** from step 1 (no new/removed sites) | FR-010/SC-005 |
| `dotnet test -c Release` | **438 passed**, 0 failed/skipped | SC-001 |
| `git diff --stat -- '*.fsi'` | empty (no `.fsi` change) | SC-004/FR-007 |
| surface-area baseline | unchanged | SC-004 |
| view JSON snapshots (step 1) | byte-identical diff | SC-004 |

## 4. Confirm the duplication is gone (the point of the change)

```bash
# hasBlocking defined once, not ~10+ times:
grep -c 'List.exists (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)' \
  src/FS.GG.SDD.Commands/CommandWorkflow.fs        # expect 1   (SC-003)

# no nested prerequisite cascade left in handlers:
#   inspect computeAnalyzePlan / computeEvidencePlan — the former 5-/6-deep
#   `match specFacts, clarificationFacts, … with Some … | _ -> [], None, None, None`
#   blocks must be gone, replaced by resolver field access     (SC-002)

# handler section net-shrinks:
git diff --stat -- src/FS.GG.SDD.Commands/CommandWorkflow.fs   # net negative (SC-006)
```

## Done when

- All step 3 checks pass (438 tests, 0 FS0025, unchanged FS3261, no `.fsi`/baseline
  diff, byte-identical view output).
- Step 4 confirms one `hasBlocking`, no handler-level prerequisite `match`, and a
  net-smaller handler section.
- No new test was added (none is permitted — behavior is unchanged) and none was
  weakened, skipped, or rewritten.
