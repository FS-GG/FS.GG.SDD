# Quickstart: verifying the `LifecycleArtifacts` split

Prerequisites: .NET SDK 10.x, repo restored (`dotnet restore`).

The single binding gate is **build + tests green**. The other checks confirm the
refactor's stated outcomes.

## 0. Capture the baseline (before refactor)

```bash
# Warning baseline (relocation, not change — FR-008/SC-005)
dotnet build -clp:NoSummary 2>&1 | grep -oE "warning FS[0-9]+" | sort | uniq -c | tee /tmp/warn-before.txt

# Test baseline (must match after — FR-004/SC-003)
dotnet test --nologo | tee /tmp/tests-before.txt   # expect 437 passing at baseline
```

## 1. Build the refactored tree (FR-007)

```bash
dotnet build
# Expect: Build succeeded, no NEW errors, compile order resolves forward refs.
```

## 2. Run the full test suite — the gate (FR-004 / SC-003)

```bash
dotnet test --nologo
# Expect: same pass count as /tmp/tests-before.txt, zero test-source edits required
#         beyond mechanical `open`/qualifier updates.
```

## 3. Per-family split present (FR-001 / SC-001 / SC-006)

```bash
ls src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
# Expect: Internal.fs, Core.fs(i), Config.fs(i), WorkItemMetadata.fs(i),
#         Specification.fs(i), Clarification.fs(i), Checklist.fs(i), Plan.fs(i),
#         RequirementModel.fs(i), Task.fs(i), Analysis.fs(i), Evidence.fs(i),
#         Verify.fs(i), Ship.fs(i), Guidance.fs(i), WorkItem.fs(i)
# And the old monolith is gone:
test ! -f src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs && echo "monolith removed"
```

## 4. Largest file well under target (FR-009 / SC-001)

```bash
wc -l src/FS.GG.SDD.Artifacts/LifecycleArtifacts/*.fs | sort -n | tail -3
# Expect: largest .fs comfortably <= ~700 lines (was 3,161).
```

## 5. Warning relocation, not change (FR-008 / SC-005)

```bash
dotnet build -clp:NoSummary 2>&1 | grep -oE "warning FS[0-9]+" | sort | uniq -c > /tmp/warn-after.txt
diff /tmp/warn-before.txt /tmp/warn-after.txt && echo "warning counts unchanged"
# FS3261/FS0025 view-parser sites should now resolve to Analysis.fs / Verify.fs / Ship.fs.
dotnet build -clp:NoSummary 2>&1 | grep -E "warning (FS3261|FS0025)" | grep -oE "LifecycleArtifacts/[A-Za-z]+\.fs" | sort | uniq -c
```

## 6. No new duplication (FR-006)

Manual/diff review: shared helpers exist once in `Internal.fs` (or `Core.fs`); no
helper body is copied into two family files.

## 7. Report update (FR-010)

Confirm the R3 row in `docs/reports/2026-06-26-074428-refactor-analysis.md` is
flipped from 🔴 to complete with a link to this feature's readiness/evidence.

## Notes

- Byte-identical artifact output is **not** separately verified — relaxed by
  stakeholder; step 2 (tests) is authoritative.
- The `LifecycleArtifacts.<member>` qualified name is **not** preserved — there are
  no external consumers; in-repo references move to `open FS.GG.SDD.Artifacts`.
