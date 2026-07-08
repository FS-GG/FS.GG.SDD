# Quickstart / Validation Guide: 090 — Plan Upstream Snapshot

Proves the feature end-to-end against a real work item on a real filesystem. See
[`contracts/plan-accept-upstream.md`](contracts/plan-accept-upstream.md) for the full cell table
and [`data-model.md`](data-model.md) for the state transitions.

## Prerequisites

```sh
cd /path/to/FS.GG.SDD
dotnet build
```

## Build and unit-test

```sh
dotnet build
dotnet test
```

Expected: green. The new coverage lives in `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs`.

## End-to-end: the reported failure, before and after

Drive the real CLI over a scratch workspace and observe the two paths that matter.

```sh
WS=$(mktemp -d) && cd "$WS"
dotnet run --project /path/to/FS.GG.SDD/src/FS.GG.SDD.Cli -- init
# ... charter -> specify -> clarify -> checklist -> plan, per fs-gg-sdd-lifecycle
```

### 1. Baseline — a clean re-run is a no-op

```sh
fsgg-sdd plan --text
```

Expected: `outcome: noChange`, exit `0`, no `stalePlanSnapshot`.

### 2. The backward edit — `plan` blocks and writes nothing

Edit `work/<id>/spec.md` (fix a requirement), then:

```sh
sha256sum work/<id>/plan.md          # record
fsgg-sdd plan --text ; echo "exit=$?"
sha256sum work/<id>/plan.md          # must be unchanged
```

Expected:
- `outcome: blocked`, `exit=1`, report on **stderr**.
- one `stalePlanSnapshot` error whose `relatedIds` is exactly `work/<id>/spec.md`.
- `changedArtifacts: 0`.
- `plan.md` digest **identical** to the recorded one — no `PD-###` line, no digest rewrite.

This is the regression the feature exists to prevent: before the change, this run exits `0`,
appends a synthesized `PD-### … stale:` line to `## Plan Decisions`, and leaves the digests stale.

### 3. Downstream does not inherit a stale plan

Without re-running `plan`:

```sh
fsgg-sdd tasks   --text ; echo "exit=$?"
fsgg-sdd analyze --text ; echo "exit=$?"
```

Expected: both `blocked`, `exit=1`, `stalePlanSnapshot`, next action pointing at
`fsgg-sdd plan --accept-upstream`. Neither reports `failedPlanPrerequisite: Plan contains stale
decisions.` — nothing injected a `stale:` marker.

### 4. One gesture accepts the upstream

```sh
cp work/<id>/plan.md /tmp/plan.before
fsgg-sdd plan --accept-upstream --text ; echo "exit=$?"
diff /tmp/plan.before work/<id>/plan.md
```

Expected:
- `outcome: succeeded`, `exit=0`, `changedArtifacts: 1`.
- the `diff` touches **only** lines inside `## Source Snapshot`. Front matter, `## Plan Scope`,
  `## Plan Decisions`, and every other section are byte-identical. This is SC-002.

Then `fsgg-sdd tasks` proceeds normally — SC-001: zero hand-edits, one command.

### 5. `--accept-upstream` does not force a write

Introduce an unrelated blocking defect (corrupt the plan's `sourceSpec:` front-matter value), then:

```sh
fsgg-sdd plan --accept-upstream --text ; echo "exit=$?"
```

Expected: `blocked`, `exit=1`, the malformed-front-matter error, `changedArtifacts: 0`, and the
snapshot **not** rewritten. FR-006.

### 6. Idempotence and determinism

```sh
fsgg-sdd plan --accept-upstream >/dev/null   # already current
fsgg-sdd plan --text                          # noChange, exit 0
fsgg-sdd plan --json > a.json ; fsgg-sdd plan --json > b.json ; diff a.json b.json
```

Expected: `--accept-upstream` on a current snapshot is a no-op (FR-005), and repeated runs are
byte-identical (FR-014, SC-005).

## Harness checks

```sh
dotnet run --project src/FS.GG.SDD.Cli -- validate --markdown
```

Expected: no new non-passing cell (SC-005).
