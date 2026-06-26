# Quickstart: Verifying R6 (Diagnostic Builder + Serializer Unification)

This is a **behavior-preserving** refactor. Verification proves that *nothing the
contract cares about moved*: byte-identical JSON, byte-stable `.fsi` + surface
baselines, green tests, green Release gate, and a net line-count reduction.

## Prerequisites

- .NET SDK for `net10.0`; repo at a clean `main`-derived `027-…` branch.
- Capture the **pre-change baseline first**, before any edit.

## 1. Capture the pre-change baseline (do this on the unmodified tree)

```bash
# Build + run the full suite once to confirm the 438-test green starting point.
dotnet test

# Snapshot the public surface + the three named .fsi files.
git show HEAD:tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline > /tmp/r6/artifacts.baseline
git show HEAD:tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline  > /tmp/r6/commands.baseline
cp src/FS.GG.SDD.Artifacts/Serialization.fsi        /tmp/r6/
cp src/FS.GG.SDD.Commands/CommandSerialization.fsi  /tmp/r6/
cp src/FS.GG.SDD.Commands/CommandReports.fsi        /tmp/r6/

# Snapshot representative --json output (SC-004) and the work-model JSON.
# Use the existing test fixtures under tests/fixtures/lifecycle-commands/* as inputs;
# at minimum: charter, analyze, refresh, and a diagnostic-emitting failure path.
#   e.g. run each command with --json and save to /tmp/r6/<cmd>.before.json
```

## 2. Implement in story order

- **Story 1 (P1)** — collapse `CommandReports.fs` onto the error-default + family
  helpers; the 14 warning constructors keep `DiagnosticWarning`.
- **Story 2 (P2)** — add `FS.GG.SDD.Artifacts.Json.JsonWriters` (+ `.fsi`); point
  both `Serialization.fs` and `CommandSerialization.fs` at it with the correct
  `StringListOrder` per caller.

Verify Story 1 against the baseline *before* starting Story 2 (the serializer work
is checked against an already-stable baseline — spec sequencing assumption).

## 3. Byte-identical gate (the binding check — SC-004, FR-006)

```bash
# Re-emit the same representative --json and work-model JSON, then diff.
#   diff -u /tmp/r6/<cmd>.before.json /tmp/r6/<cmd>.after.json   → MUST be empty
# The per-command golden tests and ReleaseDeterminismTests enforce this in-suite:
dotnet test                       # all green, no golden churn
```
Any non-empty diff — especially in `relatedIds` ordering, digest objects, null
fields, or locations — is a regression (see research D2: `relatedIds` ordering is
parameterized precisely to avoid this).

## 4. Byte-stable surface gate (SC-005, FR-007)

```bash
git diff --exit-code -- \
  src/FS.GG.SDD.Artifacts/Serialization.fsi \
  src/FS.GG.SDD.Commands/CommandSerialization.fsi \
  src/FS.GG.SDD.Commands/CommandReports.fsi \
  tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline \
  tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline
# MUST print nothing and exit 0. The SurfaceBaselineTests also assert this in-suite;
# the new JsonWriters module lives under namespace FS.GG.SDD.Artifacts.Json, so it
# is NOT captured by the exact-namespace baseline reflection (research D1).
```

## 5. Layering + Release gate (FR-008, SC-007)

```bash
# No new Artifacts -> Commands dependency (one-way layering preserved):
grep -n "FS.GG.SDD.Commands" src/FS.GG.SDD.Artifacts/*.fsproj   # MUST be empty

# R5 Release WarningsAsErrors gate (FS3261/FS0025) stays green, no new warning category:
dotnet build -c Release
```

## 6. Duplication + size outcomes (SC-002, SC-006)

```bash
# Duplicate writer bodies across the two assemblies drop to 0:
grep -rn "let writeDiagnostic\b"  src/   # exactly one definition (in JsonWriters)
grep -rn "let writeOutputDigest\b" src/  # exactly one definition (in JsonWriters)

# Net src line count decreased (analysis est. ~90 LOC):
git diff --stat -- src/
```

## Done When

- [ ] All representative `--json` and the work-model JSON diff **empty** vs baseline.
- [ ] The three named `.fsi` files and both `PublicSurface.baseline` files unchanged.
- [ ] `dotnet test` green (438 baseline) and `dotnet build -c Release` green.
- [ ] Each unified writer body exists in exactly one place; net `src` LOC down.
