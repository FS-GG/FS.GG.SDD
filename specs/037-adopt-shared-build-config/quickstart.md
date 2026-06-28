# Quickstart: Adopt Shared Build Config — Validation

Runnable scenarios that prove adoption is correct and behavior-preserving. Each maps
to a user story and the success criteria. Run from the SDD repo root. Assumes
`FS-GG/.github` is checked out alongside (or fetched) as `<gh>`.

## Prerequisites

- .NET 10 SDK (`dotnet --version` → `10.0.x`).
- A local checkout of `FS-GG/.github` for the sync tool: `<gh>/scripts/sync-build-config.sh`.
- Clean working tree on branch `037-adopt-shared-build-config`.

## Step 0 — Capture the pre-adoption baseline (for SC-005)

Before changing anything, record the effective evaluated values to diff against
later (one representative project shown; repeat across projects or script it):

```sh
for p in src/*/ tests/*/; do
  dotnet build "$p" -getProperty:TargetFramework,Version,LangVersion,Deterministic,ContinuousIntegrationBuild,Nullable,TreatWarningsAsErrors,WarningsAsErrors 2>/dev/null
done > /tmp/sdd-build-baseline.txt
dotnet restore FS.GG.SDD.sln --force-evaluate   # baseline lockfiles
git stash --include-untracked   # or commit baseline lockfiles first; keep a reference copy
```

## Step 1 — Adopt (US1 / FR-001, FR-002, FR-003, FR-004)

```sh
# Moves hand-authored *.props → *.local.props, then writes the 3 canonical files.
<gh>/scripts/sync-build-config.sh --adopt .
```

Then hand-edit the generated `*.local.props` per [data-model.md](./data-model.md):

- **Drop** from `Directory.Build.local.props`: `Deterministic`,
  `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`,
  `RestoreLockedMode`, and the `;NU1603;NU1608` line (all now in canonical).
- **Append** F# warnings: `<WarningsAsErrors>$(WarningsAsErrors);FS3261;FS0025</WarningsAsErrors>`.
- **Drop** the `FSharp.Core` `PackageVersion` from `Directory.Packages.local.props`;
  also drop its `ManagePackageVersionsCentrally` (canonical owns it).

**Expected**: canonical `Directory.Build.props`, `Directory.Packages.props`, and
`.config/dotnet-tools.json` are byte-identical to `<gh>/dist/dotnet/`; all SDD
specifics live in the two `*.local.props` files.

## Step 2 — Clean offline build + full test suite (US1/US2 / SC-004, FR-006, FR-009)

```sh
dotnet restore FS.GG.SDD.sln          # offline, unlocked (GITHUB_ACTIONS unset)
dotnet build   FS.GG.SDD.sln -c Debug --no-restore
dotnet test    FS.GG.SDD.sln --no-build
```

**Expected**: green, with **no new warnings or errors** versus the pre-adoption
baseline. No `NU1504`/`NU1011` (no duplicate `FSharp.Core` pin) — SC-003.

## Step 3 — Effective-value & lockfile equivalence (US2/US3 / SC-005, FR-006)

```sh
# Re-capture effective values and compare to the baseline — must be identical.
for p in src/*/ tests/*/; do
  dotnet build "$p" -getProperty:TargetFramework,Version,LangVersion,Deterministic,ContinuousIntegrationBuild,Nullable,TreatWarningsAsErrors,WarningsAsErrors 2>/dev/null
done | diff - /tmp/sdd-build-baseline.txt && echo "EFFECTIVE VALUES UNCHANGED ✓"

# Transitive-pinning risk check (research Decision 3): refresh and inspect.
dotnet restore FS.GG.SDD.sln --force-evaluate
git diff -- '**/packages.lock.json'   # accept only graph-equivalent churn; commit if changed
```

**Expected**: zero effective-value diff; lockfiles either unchanged or only
graph-equivalent churn (then committed).

## Step 4 — FSharp.Core resolves to the org baseline (US3 / SC-003, FR-004)

```sh
grep -R "FSharp.Core" Directory.Packages.local.props && echo "UNEXPECTED local pin" || echo "no local FSharp.Core pin ✓"
dotnet list FS.GG.SDD.sln package | grep -i FSharp.Core   # → 10.1.301 (from org baseline)
```

## Step 5 — Drift check: green when synced, red when edited (US4 / SC-001, SC-006, FR-007)

```sh
<gh>/scripts/sync-build-config.sh --check .     # expect: ok ×3, exit 0   (SC-001)

# Prove the gate is live:
printf '\n<!-- tamper -->\n' >> Directory.Build.props
<gh>/scripts/sync-build-config.sh --check . ; echo "exit=$?"   # expect: DRIFT (differs), exit 1 (SC-006)
git checkout -- Directory.Build.props           # revert
```

## Step 6 — Locked-mode CI parity (US1 / FR-005, FR-009)

```sh
GITHUB_ACTIONS=true dotnet restore FS.GG.SDD.sln --locked-mode
```

**Expected**: succeeds against the committed lockfiles; any graph drift fails
exactly as before adoption. The per-PR `gate.yml` (restore + build + test, locked)
plus the new drift-check job pass green.

## Done when

- All three canonical files byte-identical to `<gh>/dist/dotnet/` (Step 1, Step 5).
- Steps 2–6 green; effective values and resolved graph unchanged (Steps 3–4).
- Drift check proven live (Step 5).
