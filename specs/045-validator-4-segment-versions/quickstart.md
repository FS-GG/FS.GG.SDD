# Quickstart / Validation Guide: 4-Segment Versions in the Registry Validator

Runnable scenarios proving the feature end-to-end. Details live in [spec.md](./spec.md),
[research.md](./research.md), [data-model.md](./data-model.md), and
[contracts/version-grammar.md](./contracts/version-grammar.md) — this guide does not duplicate them.

**Prerequisites**: .NET SDK `10.0.x`; repo at `/home/developer/projects/FS.GG.SDD`; `gh` authenticated
for the publish/cross-repo steps (S5–S6). Scenarios S1–S4 are offline and need no network.

## S1 — Reproduce the false positive (before the fix)

Confirms the bug the feature closes. Run the typed validator over the canonical registry shape carrying
the 4-segment `governance-reference-gate-set@1.2.1.1`.

```bash
# Before the regex widening + fixture refresh:
dotnet run --project src/FS.GG.SDD.Cli -- registry validate tests/fixtures/registry/dependencies.yml --text
```

**Expected (pre-fix, once the fixture carries the 4-segment contract):** `invalid`, two
`MalformedVersion` diagnostics (on `version` and `package-version` of `governance-reference-gate-set`),
exit `1` — matching the spec's reproduction block.

## S2 — Unit corpus: accept the 4-segment shape, keep rejecting the rest (FR-001..004, FR-007, SC-002)

The failing-before/passing-after evidence. After editing `RegistryDocumentTests.fs` (accepted `1.2.1.1`
and `1.2.1.1-preview.1`; malformed theory extended with `1.2.3.4.5`; `1.2.x.4`/`abc` retained):

```bash
dotnet test tests/FS.GG.Contracts.Tests -c Release
```

**Expected:** before the `semVerRegex` widening, the new accepted-case test fails; after, the whole suite
passes — `1.2.1.1`/`1.2.1.1-preview.1` produce no diagnostic, while `1.2.x.4`, `abc`, and `1.2.3.4.5`
still produce `MalformedVersion`.

## S3 — No regression on the existing corpus (FR-003, SC-003)

The pre-existing cases (`1`, `2`, `1.0.0`, `0.1.52-preview.1`, `1.x` range) continue to pass — they are
part of the same test run in S2; confirm none flipped to malformed.

## S4 — End-to-end: valid over the canonical file (FR-005, SC-001)

With the regex widened and `tests/fixtures/registry/dependencies.yml` refreshed to mirror the canonical
`FS-GG/.github` registry (including `governance-reference-gate-set@1.2.1.1`):

```bash
dotnet run --project src/FS.GG.SDD.Cli -- registry validate tests/fixtures/registry/dependencies.yml --text
echo "exit: $?"
```

**Expected:** `valid`, zero diagnostics, exit `0`.

## S5 — Parity with the Python authority (FR-006, FR-010, SC-005)

Run both validators on the **same canonical file** and confirm identical verdicts ("valid"/"valid").

```bash
# Typed CLI (this repo):
dotnet run --project src/FS.GG.SDD.Cli -- registry validate <canonical dependencies.yml> ; echo "typed: $?"
# Python authority (FS-GG/.github):
python3 <FS-GG/.github>/scripts/validate-registry.py <canonical dependencies.yml> ; echo "python: $?"
```

**Expected:** both exit `0` with a "valid" verdict — no behavioral disagreement, satisfying the
precondition for the FS-GG/.github#49 stand-in retirement.

## S6 — Publish & verify the coordinated bump (FR-008, FR-009, SC-004)

After bumping `FS.GG.Contracts` `1.1.0 → 1.1.1` (fsproj `<Version>` + `ContractVersion.value`/`patch`)
and the SDD line `0.2.0 → 0.2.1` (`Directory.Build.local.props` + `release-readiness.json` +
`versioning-policy.md`):

```bash
# Dispatch the two-package producer (the version input satisfies the at-least-one-line tag guard):
gh workflow run release.yml --repo FS-GG/FS.GG.SDD -f version=1.1.1

# Confirm both versions are live on the org feed (must list, not 404):
gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'   # expect 1.1.1
gh api /orgs/FS-GG/packages/nuget/FS.GG.SDD.Cli/versions   --jq '.[].name'   # expect 0.2.1
```

**Consumer install smoke (no SDD source build — SC-004):** from a clean directory, install the tool at
`0.2.1` from the org feed and run `registry validate` against the canonical file; expect "valid"/exit `0`.

## S7 — Cross-repo coherence & reporting follow-through

After S6 confirms the feed serves `1.1.1`/`0.2.1` (ordering invariant — `package-version` never ahead of
the feed):

- Advance `FS-GG/.github` `registry/dependencies.yml` `fsgg-contracts` `version`/`package-version` to
  `1.1.1` (via `cross-repo-coordination`) so the `contract-coherence` gate stays green.
- Report Contracts `1.1.1` / CLI `0.2.1` on FS-GG/FS.GG.SDD#32; set Coordination board item #32 to
  `In progress`, linked to #32 and FS-GG/.github#49.
- Downstream (separate): FS-GG/.github#49 pins `0.2.1`, swaps the gate to the typed CLI, and flips
  `registry-validator-typed` toward `coherent: true`.
