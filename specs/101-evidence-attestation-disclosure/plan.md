# Implementation Plan: The Committed Verdict Discloses Its Attestation Basis

**Feature**: 101-evidence-attestation-disclosure · **Issue**: FS.GG.SDD#398 · **Spec**: [spec.md](./spec.md)

## Approach

One fact, decided once, carried down the existing chain. Nothing is observed, nothing is run, nothing
blocks. The change is entirely additive.

```
evidence.yml declaration
   │
   │  Evidence.isObserved  ← THE ONE RULE (FR-001/002). Today: false, for every declaration.
   ▼
EvidenceDispositionDraft.Observed          (HandlersEvidence — set on the `supported` arm)
   ▼
verify.json  evidenceDispositions[].observed          (FR-003; git-ignored, but the carrier)
   ▼
ship.json    verificationReadiness.{supported,selfAttested,observed}Count   (git-ignored)
   ▼
ship-verdict.json  the same three counts              ← COMMITTED (FR-005). The one that matters.
```

`ship` already recomputes its counters from the dispositions it reads out of `verify.json`
(`HandlersShip.evidenceCount`), so carrying `observed` per-disposition means ship's counters fall out
of the existing shape — no parallel rule, and nothing for a consumer to hardcode.

## Why not a summary field on `verify.json`

`verify.json` has **no summary block** — it persists dispositions only, and `ship` recomputes. So
there is nowhere to forward a count from; the fact has to travel per-disposition or `ship` would have
to assume `supported ⟹ selfAttested`, hardcoding the very thing that must change when #350 lands
(SC-005). Per-disposition it is.

## Constitutional notes

- **Pure core.** `isObserved` is a total function over an `EvidenceDeclaration`. It performs no I/O,
  so — unlike feature 099's `File.Exists` probe (FR-003 there) — it needs **no** `CommandEffect` and
  no edge interpretation. The MVU boundary is untouched.
- **Visibility lives in `.fsi`** (Principle III). Every new record field and `val` is declared in the
  matching signature file.
- **Degrade, don't throw** (Principle VIII). A view written before this feature parses to `0` /
  `false` through the existing tolerant `Option.defaultValue` parse.

## Steps

1. **`Artifacts/LifecycleArtifacts/Evidence.fs(i)`** — add `isObserved` (the one rule) and
   `isSelfAttested`, documented with why they are functions and not constants.
2. **`Commands/CommandWorkflow/HandlersEvidence.fs`** — `EvidenceDispositionDraft.Observed`, computed
   on the `supported` arm from `isObserved`.
3. **`Commands/CommandWorkflow/HandlersVerify.fs`** — carry `Observed` onto
   `VerifyEvidenceDispositionView`, emit `observed` into `verify.json`, count the two new summary
   numbers for the report.
4. **`Artifacts/LifecycleArtifacts/Verify.fs(i)`** — `EvidenceDisposition.Observed` + tolerant parse.
5. **`Commands/CommandWorkflow/HandlersShip.fs`** — count from the dispositions; emit into
   `ship.json`'s `verificationReadiness`; re-emit `observed` on the disposition rows.
6. **`Artifacts/LifecycleArtifacts/Ship.fs(i)`** — the two summary counts + tolerant parse.
7. **`Artifacts/LifecycleArtifacts/ShipVerdict.fs(i)`** — carry the three counts into the **committed**
   verdict; update `toJson` and its line-count contract.
8. **`Commands/CommandTypes.fs(i)`, `CommandSerialization.fs`, `CommandRendering.fs`,
   `Cli/Rendering.fs`** — the counters on the `verify` and `ship` report blocks, projected
   json/text/rich.
9. **`Artifacts/ReleaseContract.fs`** — inventory the new fields (FR-010), then refresh
   `docs/release/release-readiness.json` and its test baseline.
10. **Tests + goldens + `docs/release/schema-reference.md`.**

## Verification plan

| Requirement | How it is proven |
|---|---|
| FR-001/002/003 | `observed` is present per-disposition in the `verify.json` golden; one function, grepped. |
| FR-004 | `VerificationViewTests` / `ShipViewTests` round-trip the fields; an old view parses to `0`. |
| FR-005 | `ShipVerdictTests` — the committed verdict carries the three counts. |
| FR-006 | `CommandReportJsonTests`, `TextProjectionTests`, `RichRenderingTests`. |
| FR-007 | An explicit invariant test: `supported == selfAttested + observed`. |
| FR-008 | Every existing golden's `disposition.state` and exit code unchanged — the diff proves it. |
| FR-009 | A test asserting `evidenceObservedCount = 0` on a hand-authored lifecycle. |
| FR-010 | `ReleaseConformanceTests` (full-depth key set vs `catalog[].inventory`). |

## Risks

- **Golden churn is wide but shallow.** Many goldens gain fields; none should change a state or an
  exit code. Any golden whose `disposition`/`readiness`/`severity` moves is a **bug in this feature**,
  not an expected update — review the regenerated diff for exactly that, do not rubber-stamp it.
  (This is the failure mode the feature itself is about, so it would be a poor place to be careless.)
- **`ship-verdict.json` is a committed contract.** Additive with a tolerant parse, but it is the one
  artifact already in other repos' history. Verified additive-only; `schemaVersion` stays `1`.
