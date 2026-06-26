# Quickstart / Validation Guide: Null-Clean JSON Access + Warnings-as-Errors Gate

**Feature**: 026-null-clean-json-helpers | **Date**: 2026-06-26 | **Phase**: 1

Runnable steps that prove the feature works end to end. These are the same checks
encoded as the verification contract (`contracts/warnings-gate.md` §C3) and map to
SC-001…SC-006.

## Prerequisites

- .NET SDK 10.0.x (`dotnet --version`)
- Repo restored: `dotnet restore`
- Run all commands from the repository root.

## Step 0 — Capture the baseline (before any change)

```bash
# Raw and unique FS3261, and confirm no other category exists.
dotnet build -c Release --no-incremental 2>&1 | grep -oE "warning FS[0-9]+" | sort | uniq -c
# Expected at baseline: "952 warning FS3261" and nothing else.

# Save a deterministic --json baseline for a representative command on a fixture.
# (Use an existing fixture under tests/ or a scratch work item; capture stdout.)
dotnet run --project src/FS.GG.SDD.Cli -- analyze --json <fixture-args> > /tmp/analyze.before.json
```

## Step 1 — Validate the null-cleanup (Story 1, SC-001/SC-002/SC-004)

After the null-handling edits land but **before** the gate is added:

```bash
# SC-001 / SC-002: zero nullness and zero incomplete-match warnings.
dotnet build -c Release --no-incremental 2>&1 | grep -E "warning FS3261|warning FS0025" | wc -l
# Expected: 0

# SC-004: behavior unchanged — full suite green.
dotnet test
# Expected: all 438 tests pass.

# SC-004: deterministic output byte-identical.
dotnet run --project src/FS.GG.SDD.Cli -- analyze --json <fixture-args> > /tmp/analyze.after.json
diff /tmp/analyze.before.json /tmp/analyze.after.json
# Expected: no diff (repeat for charter and refresh).
```

This slice is independently shippable: a clean warning signal with zero behavior
change, even if the gate below is never added.

## Step 2 — Add and validate the gate (Story 2, SC-003/SC-006)

Add to `Directory.Build.props` (see `contracts/warnings-gate.md` §C1):

```xml
<WarningsAsErrors>FS3261;FS0025</WarningsAsErrors>
```

```bash
# V-4: clean build still succeeds with the gate on.
dotnet build -c Release --no-incremental
# Expected: Build succeeded, 0 warnings, 0 errors.
```

### Prove the gate actually bites (SC-003)

Temporarily reintroduce one nullness defect, e.g. in `LifecycleArtifacts/Internal.fs`
revert a coalesced helper back to `Some(value.GetString())`:

```bash
dotnet build -c Release --no-incremental
# Expected: Build FAILED — "error FS3261: Nullness warning: ..." at that line.

# Revert the defect.
git checkout -- src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs
dotnet build -c Release --no-incremental
# Expected: Build succeeded again.
```

### Confirm scope (SC-006)

```bash
# No category other than FS3261/FS0025 is promoted to an error.
dotnet build -c Release --no-incremental 2>&1 | grep -E "error FS" | grep -vE "FS3261|FS0025" | wc -l
# Expected: 0
```

## Done when

- [ ] Step 1: 0 FS3261 + 0 FS0025; all tests pass; `--json` diffs empty.
- [ ] Step 2: gate added; clean build succeeds; injected defect fails the build;
      reverting restores green; no off-scope category fails.
- [ ] `data-model.md` "Enumerated suppressions" is still empty (or lists every
      intractable site with justification).

## References

- Spec: [spec.md](./spec.md) (FR-001…FR-009, SC-001…SC-006)
- Decisions: [research.md](./research.md) (D1–D7)
- Boundary map & invariants: [data-model.md](./data-model.md)
- Build + null-handling contract: [contracts/warnings-gate.md](./contracts/warnings-gate.md)
