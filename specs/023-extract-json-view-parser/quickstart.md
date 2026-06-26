# Quickstart / Validation: shared JSON view-parser skeleton (R4)

Validation guide for the `parseJsonView` extraction. All scenarios are
regression checks — the binding gate is **build green + the existing 437-test
suite green + zero FS0025**. See [spec.md](./spec.md) for FR/SC IDs,
[contracts/parse-json-view.md](./contracts/parse-json-view.md) for the skeleton
contract, and [data-model.md](./data-model.md) for the (unchanged) types.

## Prerequisites

- .NET SDK `10.0.x` (repo baseline: 10.0.301).
- Clean working tree on branch `023-extract-json-view-parser`.
- Capture the **baseline** before any edit (for FR-009 / SC-001 / SC-002 deltas):

```bash
# FS0025 count (expect 4 before, 0 after) and FS3261 count (must be unchanged)
dotnet build -c Release --no-incremental 2>&1 | tee /tmp/build-before.log
grep -c FS0025 /tmp/build-before.log
grep -c FS3261 /tmp/build-before.log
dotnet test 2>&1 | tee /tmp/test-before.log   # expect 437 passed
```

## Scenario 1 — Zero FS0025, build green (US2 / FR-005 / SC-001)

```bash
dotnet build -c Release --no-incremental 2>&1 | tee /tmp/build-after.log
grep -c FS0025 /tmp/build-after.log    # expect: 0  (down from 4)
```

**Expected**: build succeeds; **0** FS0025 incomplete-match warnings across `src`.

## Scenario 2 — FS3261 count is unchanged (FR-009)

```bash
grep -c FS3261 /tmp/build-after.log
# Compare against /tmp/build-before.log — counts MUST be equal (relocation only,
# no new or removed nullness sites).
```

**Expected**: identical FS3261 count before and after.

## Scenario 3 — Full suite still green, unchanged (US1 / FR-006 / FR-008 / SC-002)

```bash
dotnet test 2>&1 | tee /tmp/test-after.log    # expect 437 passed, 0 failed
```

**Expected**: **437** tests pass — equal to baseline — with no test source changes
beyond mechanical call-site updates (none expected; public signatures are
unchanged). The analysis / verify / ship / agent-guidance view-parser suites pass
unchanged, proving byte-identical parse results, ordering, and diagnostics for
every fixture (SC-004).

## Scenario 4 — Skeleton defined once, no copied bodies (US1 / FR-001 / FR-007 / SC-003)

```bash
# The parse skeleton (try/JsonDocument.Parse + classifyRaw + schema-error arms)
# should appear ONCE — in Internal.fs — and be referenced by all four parsers.
grep -rn "parseJsonView" src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
# Expect: 1 definition in Internal.fs + 4 call sites (Analysis/Verify/Ship/Guidance).

# No parser retains its own classifyRaw skeleton match:
grep -rn "SchemaVersion.classifyRaw" src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Analysis.fs \
  src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Verify.fs \
  src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fs \
  src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Guidance.fs
# Expect: no hits (classifyRaw now lives only in Internal.parseJsonView).
```

**Expected**: one `parseJsonView` definition + four call sites; no parser body
contains the duplicated skeleton; net `src` shrinks by ~70 LOC.

## Scenario 5 — No public `.fsi` changed (FR-007 / SC-004)

```bash
git diff --name-only -- 'src/**/*.fsi'
# Expect: EMPTY — no .fsi file is modified by this refactor.
```

**Expected**: no `.fsi` diff; the four `val parse…View` / `val parseGeneratedAgentGuidance`
signatures are byte-stable.

## Scenario 6 — Impossible schema state degrades, never throws (US2 / FR-003 / FR-004 / SC-005)

Optional totality assertion. Construct a `SchemaCompatibility` with
`Version = None` and `Status = Current` (the previously-unreachable state) and
confirm any view parser — or `parseJsonView` directly — returns a
malformed-schema-version `Error` rather than raising `MatchFailureException`.

```fsharp
// xUnit (illustrative — exact wiring is an implementation detail):
// feed a snapshot whose schemaVersion forces (Version = None, status current/deprecated),
// or unit-test parseJsonView's match arm directly, and assert:
//   result = Error [ malformedSchemaVersion artifact
//                      "<label> is missing or has malformed schemaVersion." ]
// and that no exception is raised.
```

**Expected**: returns the malformed-schema-version `Error`; no exception. This is
the only genuinely new behavior and the only new test permitted by the spec.

## Done when

- [ ] Scenario 1: 0 FS0025 (was 4).
- [ ] Scenario 2: FS3261 count unchanged.
- [ ] Scenario 3: 437 tests pass, unchanged.
- [ ] Scenario 4: `parseJsonView` defined once + 4 call sites; no copied skeletons.
- [ ] Scenario 5: no `.fsi` diff.
- [ ] Scenario 6: impossible state returns a diagnostic `Error`, never raises.
