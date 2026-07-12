# Tasks: The Committed Verdict Discloses Its Attestation Basis

**Feature**: 101-evidence-attestation-disclosure · **Issue**: FS.GG.SDD#398

| # | Task | Requirement | Status |
|---|---|---|---|
| T001 | `Evidence.isObserved` / `isSelfAttested` — the rule, stated once in `Artifacts` | FR-001, FR-002 | done |
| T002 | `EvidenceDispositionDraft.Observed`, set on the `supported` arm | FR-003 | done |
| T003 | `VerifyEvidenceDispositionView.Observed`; emit `observed` into `verify.json` | FR-003 | done |
| T004 | `Verify.EvidenceDisposition.Observed` + tolerant parse | FR-003, FR-004 | done |
| T005 | `ship` counts the basis `verify` recorded; emit into `ship.json` | FR-003, FR-004 | done |
| T006 | `Ship.ShipVerificationReadinessSummary` counts + tolerant parse | FR-004 | done |
| T007 | **`ShipVerdict` carries the three counts into the committed verdict** | FR-005 | done |
| T008 | Report counters on the `verify` + `ship` blocks; json / text / rich | FR-006 | done |
| T009 | `ReleaseContract` inventory + regenerated `release-readiness.json` ×2 | FR-010 | done |
| T010 | `docs/release/schema-reference.md` — what a green verdict means | FR-005 | done |
| T011 | Tests: the invariant, the standing `observed == 0` proof, the committed verdict | FR-007, FR-009 | done |
| T012 | Regenerate goldens; audit that no state / verdict / severity moved | FR-008 | done |
| T013 | Mirror the disclosure onto the `TD-` required-test dispositions | FR-011 | done |

## Verification performed

Not asserted — **run**, which is the least this feature of all features could do.

The epic's own ninety-second probe (`.github`#417), reproduced against the real CLI: a lifecycle
walked on scaffolded boilerplate, five obligations declared `result: pass` / `synthetic: false`
citing a file created with `echo` and executed by nothing.

```
$ fsgg-sdd verify --work 001-probe --text
verifyEvidenceSupported: 5
verifyEvidenceSelfAttested: 5
verifyEvidenceObserved: 0        ← the disclosure
verifyTestSatisfied: 5
verifyTestSelfAttested: 5
verifyTestObserved: 0            ← and on the counter that NAMES a test (FR-011)

$ fsgg-sdd ship --work 001-probe --text
outcome: succeeded
shipReadiness: shipReady
shipEvidenceSupported: 5
shipEvidenceSelfAttested: 5
shipEvidenceObserved: 0
```

and in `readiness/001-probe/ship-verdict.json` — the one artifact that reaches git:

```json
"verificationReadiness": {
  "status": "verificationReady",
  "evidenceSupportedCount": 5,
  "evidenceSelfAttestedCount": 5,
  "evidenceObservedCount": 0
},
"readiness": "shipReady"
```

`ship` still succeeds, which is correct: this feature discloses and **blocks nothing** (FR-008). The
green is still green — it has simply stopped implying something it never checked.

Two things also fell out of the walk, both good news and neither this feature's doing:

- **#351's gate held.** The pure-boilerplate lifecycle **cannot** reach `analyze` any more —
  `unauthoredScaffoldContent` blocks on `PD/PC/VO/PM/GV-001`. Reproducing the probe required
  authoring four real plan decisions first.
- **#349's gate held.** The cited artifact had to actually exist on disk. It just had to *exist* —
  which is precisely the gap #350 names, and precisely what `evidenceObservedCount: 0` now says.

Full suite: **1,662 passed, 0 failed.** Fantomas clean.
