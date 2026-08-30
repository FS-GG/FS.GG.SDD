# 1.5.1 candidate qualification

This record bounds the protected-main anomaly and records the release/process
controls used for the 1.5.1 candidate. It is evidence, not publication authority.

## Protected-main anomaly

- Run [33299790063 attempt 1](https://github.com/FS-GG/FS.GG.SDD/actions/runs/33299790063/attempts/1)
  at exact commit `88d2be0b2d83bed3398480edab1545a7b99fae88` failed only
  `RefreshEvidenceDeadlockTests.seeding appends and leaves every authored declaration byte-identical`:
  its non-vacuity assertion observed no seeded declarations. The other 1,340
  Commands tests and every other project passed.
- [Attempt 2](https://github.com/FS-GG/FS.GG.SDD/actions/runs/33299790063/attempts/2)
  on the identical commit and test bytes passed the complete protected-main gate.
- After a locked restore and Debug build, the exact test passed in 20 separate
  processes (20 passed, 0 failed). The sorted SHA-256 listing of those 20 TRX
  reports was
  `0a6ea39c329dd3478427a194083433ec383a908b34b3b8c14df9f1da462aeb98`;
  the test source SHA-256 remained
  `5f8711af4db5302d2302c887803c66af8071eb885b9628bda2334c113dfa92a8`.
- The 1.5.1 candidate then passed the complete Debug and Release suites, each
  with 2,452 passes and 5 expected network-gated skips. The assertion remains
  unchanged. This bounds the event as a non-reproducing one-off; there is no
  evidence for a speculative product or test weakening.

## Release controls

- `FS.GG.SDD.Artifacts` and `FS.GG.SDD.Cli` evaluate to 1.5.1;
  `FS.GG.Contracts` remains 7.5.2.
- The version-policy inversion changed only the documented current version to
  1.5.0. T012 failed by requiring 1.5.1, then passed after restoration.
- ApiCompat passed against Contracts 7.5.2. Both source-surface axes and the
  dependency surface were coherent. Exhaustive validation reported 287 passed,
  0 failed, 0 coverage gaps, and 0 not validated.
- A clean local install from the packed 1.5.1 CLI reported version 1.5.1. The
  installed executable accepted tracked work-package evidence, refused ignored
  and untracked local artifacts with `evidence.localArtifactNotTracked`, and
  accepted an explicit durable external URI receipt.
- Package identities, exact candidate commit, and final SHA-256 digests are
  emitted in the typed delivery handoff after the final head is packed once.

## Process collision

At 08:00 UTC the #944 claim exposed an overlap with #937, whose prior claim and
PR #940 had been paused since 2026-08-28. A direct precedence message was sent at
08:01. The required path widening was refused at 08:06 because the stale claim
still owned the intersection. At 08:08 the expired claim was force-collected;
#940 was closed recoverably with its branch/history retained, #937 was placed in
Blocked with a real dependency on #944, and the active overlap check became
disjoint. No product scope or non-vacuity control was weakened to optimize this
timing experiment. The event demonstrates that claim expiry and an explicitly
recorded precedence/dependency transition are necessary before a critical
release lane can safely widen its touch set.

## Bounded architecture review

The two related qualification findings triggered a bounded state-boundary review
before PR creation. The mapping, sibling inventory, causal classification,
repair, and discriminating positive/inversion controls are recorded in
`work/944-sdd-151-evidence-authority-release/architecture-review.md`. ApiCompat's
restore/build state is now hermetic; the protected-main seeding anomaly remains
bounded and causally separate on the available evidence.
