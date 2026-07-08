# Quickstart: verifying the authored-artifact codec

How to prove feature 096 works end-to-end. Assumes the source refactor (Phase 2,
Blocked by #189) has landed. Each scenario maps to a spec user story and a
closed issue.

## Prerequisites

- A built `fsgg-sdd` (`dotnet build FS.GG.SDD.sln -c Debug`).
- A scratch workspace: `W=$(mktemp -d); fsgg-sdd init --root "$W"`.

## Scenario 1 — authored provenance survives a re-run (US1 / #181)

```sh
mkdir -p "$W/work/001-demo"
cat > "$W/work/001-demo/evidence.yml" <<'YAML'
schemaVersion: 1
workId: 001-demo
evidence:
  - id: EV001
    kind: test-output
    subject: { type: task, id: T001 }
    result: pass
    sourceRefs:
      - id: SR-1
        kind: test-output
        path: readiness/tests.txt
        digest: 9f2b0000
        relatedSourceId: T001
        result: pass
lifecycleNotes:
  - Ran the headless suite by hand.
YAML

fsgg-sdd evidence --work 001-demo --root "$W" --json >/dev/null
grep -q 'id: SR-1'            "$W/work/001-demo/evidence.yml"   # was deleted before
grep -q 'digest: 9f2b0000'   "$W/work/001-demo/evidence.yml"   # was deleted before
grep -q 'relatedSourceId: T001' "$W/work/001-demo/evidence.yml" # was deleted before
grep -q 'Ran the headless suite by hand' "$W/work/001-demo/evidence.yml" # was clobbered
```

**Expected**: all four `grep`s succeed. Before the fix, the three `sourceRef`
fields and the authored `lifecycleNotes` line are gone after the first re-run.

## Scenario 2 — absence stays absence (US2 / #182)

```sh
# a snapshot with no digest/schemaVersion must not re-render as "" / 1
fsgg-sdd evidence --work 001-demo --root "$W" --json >/dev/null
! grep -E 'digest: *$' "$W/work/001-demo/evidence.yml"   # no empty-value line
```

**Expected**: no `digest:` line with an empty value; no invented `schemaVersion: 1`
on a snapshot that declared none.

## Scenario 3 — bare-null disclosure blocks at both stages (US3 / #180)

```sh
cat > "$W/work/001-demo/evidence.yml" <<'YAML'
schemaVersion: 1
workId: 001-demo
evidence:
  - id: EV001
    kind: synthetic
    subject: { type: task, id: T001 }
    result: pass
    synthetic: true
    syntheticDisclosure:
      standsInFor: null
      reason: null
YAML

fsgg-sdd evidence --work 001-demo --root "$W" --json | jq -e '.outcome=="blocked"'
fsgg-sdd evidence --work 001-demo --root "$W" --json \
  | jq -e '[.diagnostics[].id] | index("evidence.undisclosedSyntheticEvidence")'
fsgg-sdd verify   --work 001-demo --root "$W" --json | jq -e '.outcome=="blocked"'  # parity
```

**Expected**: `evidence` blocks (exit 1) with
`evidence.undisclosedSyntheticEvidence`; `verify` blocks too. Before the fix both
pass at exit 0 — synthetic evidence masquerading as real.

## Scenario 4 — tasks.yml title + impact survive a re-run (US1 / Gap-A unfiled)

```sh
# author a custom title and publicOrToolFacingImpact: false, then re-run tasks
sed -i 's/^  title:.*/  title: My hand-written title/' "$W/work/001-demo/tasks.yml"
sed -i 's/^  publicOrToolFacingImpact:.*/  publicOrToolFacingImpact: false/' "$W/work/001-demo/tasks.yml"
fsgg-sdd tasks --work 001-demo --root "$W" --json >/dev/null
grep -q 'title: My hand-written title'      "$W/work/001-demo/tasks.yml"
grep -q 'publicOrToolFacingImpact: false'   "$W/work/001-demo/tasks.yml"
```

**Expected**: both survive. Before the fix the title reverts to the humanized id
and the flag flips to `true`.

## Scenario 5 — the property (US4 / FR-005)

```sh
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter 'FullyQualifiedName~RoundTrip'
```

**Expected**: the FsCheck properties `parse(render(m)) = m` for `evidence.yml`
and `tasks.yml` pass over generated models. Deliberately deleting a `Write` from
one `FieldCodec` reddens the property (the demonstration for SC-002).

## Scenario 6 — the coupling test (FR-007)

```sh
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter 'FullyQualifiedName~ArtifactCodec'
```

**Expected**: passes. Adding an authored field to the record with no `FieldCodec`
entry fails it (SC-004).
