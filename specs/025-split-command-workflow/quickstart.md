# Quickstart: Validate the CommandWorkflow split

This proves the refactor is behavior-preserving. Run from the repo root after the
split lands (or after each incremental commit). All steps are mechanical; the
existing 438-test suite is the behavioral guard.

## Prerequisites

- .NET SDK 10.0.x (matches the R2 baseline build `0.2.0`, SDK 10.0.301).
- A clean working tree on `025-split-command-workflow`.
- `BASE=$(git merge-base main HEAD)` — the immutable pre-refactor commit
  (recorded in task T001). Diff against this fixed `$BASE`, **not** the bare
  `main` ref, which moves as the branch is rebased or `main` advances.

## 1. Public signature is byte-identical (C-1 / FR-002)

```bash
BASE=$(git merge-base main HEAD)
git diff --exit-code "$BASE" -- src/FS.GG.SDD.Commands/CommandWorkflow.fsi
echo "exit=$?  # MUST be 0 with no diff output"
```

## 2. Release build is clean, no new warnings (C-3 / FR-007)

```bash
dotnet build -c Release --no-incremental 2>&1 | tee /tmp/r2-build.log
# Expect: Build succeeded. 0 Error(s).
# FS3261 unique-site count must not exceed the ~290 src baseline:
grep -oE '[^ ]+\.fs\([0-9]+,[0-9]+\): warning FS3261' /tmp/r2-build.log \
  | sort -u | wc -l
```

## 3. Full suite passes, no fixtures regenerated (C-2 / FR-003, FR-009)

```bash
dotnet test
# Expect: Passed!  ... 438 tests.
git status --porcelain
# MUST be empty (no golden / baseline / surface / release-readiness file edits).
```

## 4. File-size cap & layout (L-3 / FR-004, SC-001)

```bash
wc -l src/FS.GG.SDD.Commands/CommandWorkflow.fs \
      src/FS.GG.SDD.Commands/CommandWorkflow/*.fs | sort -n
# No single file may exceed ~1,500 lines. The old 6,814-line monolith is gone;
# the facade CommandWorkflow.fs is ~150 lines.
```

## 5. Layering preserved (C-4 / FR-008)

```bash
grep ProjectReference src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj
# Only ../FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj — unchanged, no cycle.
```

## 6. Spot-check behavioral equivalence on a real work item (C-2/C-5)

Run a representative command before (at the immutable `$BASE`) and after,
comparing JSON bytes:

```bash
BASE=$(git merge-base main HEAD)
# Example: verify projection for an existing readiness id (adjust id to a fixture)
dotnet run --project src/FS.GG.SDD.Cli -- verify <work-id> --json > /tmp/after.json
git stash; git checkout "$BASE"
dotnet run --project src/FS.GG.SDD.Cli -- verify <work-id> --json > /tmp/before.json
git checkout 025-split-command-workflow; git stash pop 2>/dev/null || true
diff /tmp/before.json /tmp/after.json && echo "BYTE-IDENTICAL"
```

(The deterministic suite in step 3 is the authoritative proxy; this step is a
manual confirmation, especially for `refresh`, whose handler keeps its own guard.)

## Done when

- Steps 1–5 all green; step 6 shows byte-identical output.
- `docs/reports/2026-06-26-074428-refactor-analysis.md` R2 row is marked ✅ with
  the commit/spec evidence and the aggregate count updated (FR-010).
