# Quickstart: Correct capabilities schema version to 2 and republish 1.0.1

Runnable validation that proves the correction end-to-end. Run from the repo root
(`/home/developer/projects/FS.GG.SDD`). See [contracts/](./contracts/) and
[data-model.md](./data-model.md) for the authoritative details.

## Prerequisites

- .NET SDK with `net10.0` support; `dotnet` on `PATH`.
- The branch `040-correct-capabilities-version` checked out.

## Scenario A — Verification suite asserts `capabilities = 2` (US1 / SC-001)

```bash
dotnet test tests/FS.GG.Contracts.Tests
```

**Expected**: green. The fact *"Governance-owned schema versions equal the declared
reference values"* asserts `capabilitiesVersion = 2` and `governance`/`policy`/
`tooling = 1`. The SDD-owned-constants, `entries`, owner, and `PublicSurface`
baseline facts also pass unchanged (proves FR-002 / FR-007: no SDD emission or
surface drift).

## Scenario B — Pack the new immutable identity 1.0.1 (US2 / FR-004)

```bash
TMP="$(mktemp -d)"
dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release \
  -p:Version=1.0.1 -o "$TMP/packages" --nologo
ls -1 "$TMP/packages"
# expect: FS.GG.Contracts.1.0.1.nupkg
```

**Expected**: exactly one `FS.GG.Contracts.1.0.1.nupkg`. `1.0.0` is not touched.

## Scenario C — Publish to and resolve from the shared local folder feed (US2 / SC-002)

```bash
# Push into the committed local folder feed (source key from nuget.config)
dotnet nuget push "$TMP/packages/FS.GG.Contracts.1.0.1.nupkg" --source fsgg-local

# Confirm 1.0.1 is present in the feed layout
find .fsgg-local-feed -iname 'FS.GG.Contracts*1.0.1*'
```

**Expected**: `1.0.1` appears under `.fsgg-local-feed/`. A consumer pointing a
`nuget.config` at this feed resolves `FS.GG.Contracts 1.0.1`, and reading
`Fsgg.Schemas.capabilitiesVersion` returns `2` — single-sourced, no local literal
(SC-005). `1.0.0`, if previously published anywhere, is unchanged.

## Scenario D — Restore stays clean with the new feed source (regression)

```bash
dotnet restore FS.GG.SDD.sln 2>&1 | grep -iE 'error|unable to load the service index' || echo "restore clean"
```

**Expected**: `restore clean` — the committed empty `.fsgg-local-feed/` keeps the
configured source path valid; inherited sources (nuget.org) still resolve.

## Scenario E — Cross-repo registry pin follow-on (US3 / SC-003) — out-of-repo

Not validated by a command in this repo. After publishing `1.0.1`, advance the
`fsgg-contracts` pin `1.0.0` → `1.0.1` in `FS-GG/.github` via a coordination
issue + PR (`owner: sdd`); confirm the contract-coherence workflow passes there.
See [contracts/delivery.md](./contracts/delivery.md).

## Done when

- [ ] Scenario A green (constant = 2, siblings = 1, suite passes).
- [ ] Scenario B produces exactly `FS.GG.Contracts.1.0.1.nupkg`.
- [ ] Scenario C resolves `1.0.1` from the local feed carrying `capabilities = 2`.
- [ ] Scenario D restore is clean.
- [ ] Scenario E tracked as a cross-repo follow-on (not blocking this repo's merge).
