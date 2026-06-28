# Quickstart / Verification: Publish FS.GG.Contracts 1.1.0

This is the operational runbook for the feature. There is no in-repo unit test (Principle VI
note in plan.md); verification is the publish workflow run, a real feed query, the registry
advance, and the new doc's presence. The publish path itself is feature 039's `release.yml`,
invoked unchanged.

## Prerequisites

- `.github/workflows/release.yml` present on the canonical repo `FS-GG/FS.GG.SDD` (feature 039).
- `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` `<Version>` == `1.1.0` (set by feature 042).
- `gh` authenticated; `read:packages` for the feed query; ability to dispatch the workflow.
- `dotnet` SDK `10.0.x` locally for the offline pre-flight check.

## C0 — Confirm the source is at 1.1.0 (local, no push)

```bash
dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version
# expect: 1.1.0
grep -n 'let value' src/FS.GG.Contracts/ContractVersion.fs
# expect: let value = "1.1.0"
```

## C1 — Confirm the feed is currently a version behind (FR-001 baseline)

```bash
gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'
# expect (before): 1.0.1     (only — 1.1.0 is the gap this feature closes)
```

## C2 — (Optional) dry run — packs 1.1.0, pushes nothing

```bash
gh workflow run release.yml --repo FS-GG/FS.GG.SDD     # no version input ⇒ dry run
```
Expected: the `publish` job logs a dry-run notice, lists `Packed: FS.GG.Contracts.1.1.0.nupkg`,
and **skips** the push. The feed is unchanged.

## C3 — Publish 1.1.0 (US1 / FR-001, FR-002, FR-003)

```bash
gh workflow run release.yml --repo FS-GG/FS.GG.SDD -f version=1.1.0
```
Expected: contracts tests pass (the publish gate, FR-002), pack produces one `.nupkg`, and
`nuget push --skip-duplicate` lands `1.1.0` on the org feed. Re-running is an idempotent success
(FR-003).

## C4 — Verify 1.1.0 is obtainable from the feed (SC-001)

```bash
gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'
# expect (after): 1.1.0 and 1.0.1   (1.1.0 now present, not 404)
```
A clean authorized consumer can `dotnet restore` `FS.GG.Contracts` at exactly `1.1.0` from the
org feed.

## C5 — Advance the registry package-version (US2 / FR-004, cross-repo, AFTER C4)

Only after C4 confirms 1.1.0 is live (FR-007: never ahead of the feed), notify the registry
coordinator:

```bash
gh issue comment 42 --repo FS-GG/.github \
  --body "## Response — FS.GG.Contracts 1.1.0 is live on the org feed. Please advance registry fsgg-contracts.package-version 1.0.1→1.1.0 (version pin already 1.1.0). Source/feed now coherent at 1.1.0."
# FS-GG/.github#42 is the .github-side registry issue (NOT the SDD issue #27);
# use its successor if #42 is already closed.
```
Expected: `FS-GG/.github` `registry/dependencies.yml` records `fsgg-contracts.package-version:
1.1.0` and its `docs/registry/compatibility.md` projection agrees; the `contract-coherence` gate
stays green (SC-003).

## C6 — Confirm the durable checklist exists (US3 / FR-005, SC-004)

```bash
test -f docs/release/contracts-version-bump-checklist.md && \
  grep -Eqi 'publish' docs/release/contracts-version-bump-checklist.md && \
  grep -Eqi 'package-version' docs/release/contracts-version-bump-checklist.md && \
  grep -Eqi 'ADR-0001|contract-coherence' docs/release/contracts-version-bump-checklist.md && \
  echo "checklist present and names the three same-change actions"
```
Expected: the runbook exists and names bump-source · publish-to-feed · update-registry, citing
the `contract-coherence` gate / ADR-0001.

## C7 — Confirm no contract/CLI/golden drift (SC-005)

```bash
dotnet test FS.GG.SDD.sln -c Release      # existing suite incl. 042 registry-validator goldens
git status --porcelain                     # only docs/release/contracts-version-bump-checklist.md (+ specs/) changed
```
Expected: full suite green; the only committed in-repo change is the new doc — no `src/`,
`tests/fixtures/registry/dependencies.yml`, workflow, or golden change.
