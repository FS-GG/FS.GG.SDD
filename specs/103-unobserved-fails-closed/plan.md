# Implementation Plan: An Unobserved Pass Fails Closed

**Spec**: `specs/103-unobserved-fails-closed/spec.md` · **Issue**: FS.GG.SDD#350 · **ADR**: ADR-0035

## Architecture

Spec 102 built every piece this feature needs and deliberately wired none of them to a verdict. The
receipt is recorded, `Evidence.obligationIsObserved` reads it, and `verify` / `ship` / the committed
verdict already *count* it. What is missing is the single thing ADR-0035 stage 3 names: the count has
no consequence.

So this feature adds **no new concept**. It adds a disposition, a flag, and two blocking diagnostics.

```
verify --require-observed
  │
  └─ HandlersVerify.verifyTestDispositionViews  (requireObserved: bool)
        └─ TD- ladder, arm inserted IMMEDIATELY above `satisfied`:
              requireObserved ∧ (a real pass exists) ∧ ¬obligationIsObserved matches
                 → "unobserved", [ verify.unobservedRequiredTest ]
        └─ dispositionSeverity "unobserved" = "blocking"  → hasBlocking
                 → readiness = needsVerificationCorrection

ship --require-observed
  │
  └─ HandlersShip.shipVerificationPrerequisite  (requireObserved: bool)
        └─ reads the RECORD verify wrote (shipEvidenceAttestationCounts)
              selfAttested > 0  → ship.unobservedEvidence (blocking)
                 → readiness = needsShipCorrection
```

**The ladder ordering is load-bearing, not cosmetic.** The arm sits directly above `satisfied` so it
intercepts exactly the passes that would have reached it, and *below* the `synthetic` and deferral
arms so neither is punished for a run it never asserted (FR-007).

**`obligationIsObserved` is consumed, never restated** (FR-004). Its `forall`-over-real-passes reading
— one observed run beside one hand-asserted pass is *not* observed — is inherited for free by negating
it. Restating the rule here is how `ED-` and `TD-` would drift on what "observed" means.

## Why `ship` takes the flag too — the fail-open a green suite could not see

The obvious design is a flag on `verify` alone: `ship` already refuses a `verify.json` that is not
`verificationReady`, so the gate should propagate for free. **That is wrong, and the first version of
this change shipped it.**

Driving the real CLI end-to-end exposed it in one command:

```
verify                      -> verificationReady   (writes verify.json)
verify --require-observed   -> BLOCKED             (writes nothing — correctly!)
ship                        -> shipReady           ← reads the STALE GREEN verify.json
```

A blocked stage writes nothing, by constitutional design, so the *previous* green record survives —
and every source digest still matches it, so no staleness check fires. Nothing downstream can see that
the gate ever fired. **This is #266's own rule failing inside the fix for #266**, and all 1,715 tests
were green while it was true: the unit tests never wrote a green `verify.json` first, so `ship` blocked
on an *absent* prerequisite and the assertion passed for the wrong reason.

Hence `ship` re-asserts the receipt against the record `verify` wrote (FR-005). Two switches, one
shared rule, and neither trusts the other's verdict.

## The default stays off

`RequireObserved = false` everywhere it is constructed. With the flag absent, `verifyTestDispositionViews`
is byte-for-byte its old self (the arm is unreachable) and `shipVerificationPrerequisite` adds an empty
list. No golden moves; no persisted schema changes; the `governance-handoff` surface gains only a
*reachable-under-flag* disposition value.

Flipping the default is a human's call, on a schema major, once the fleet records receipts (spec §"Why
this ships opt-in"). The test `--require-observed is opt-in - the default still satisfies an unobserved
pass` is the tripwire: it is designed to go **deliberately red** at that moment.

## Verification plan

Tests alone are insufficient here and this plan says so: the load-bearing fail-open above was
**invisible to a fully green suite** and was found by driving the binary. Both are therefore required.

1. **Failure leg (AC-001)** — the #350 fabricated lifecycle must not reach `shipReady`. Mutation-checked:
   with the `unobserved` arm disabled, this test must go red.
2. **Stale-record leg (AC-004)** — a green `verify.json` from an earlier unflagged run must not launder
   an unobserved pass past `ship`. Mutation-checked against removal of the ship gate.
3. **Non-brick leg (AC-002)** — a recorded receipt still reaches `shipReady`.
4. **Opt-in leg (AC-003)** — default behavior byte-identical.
5. **No-punishment leg (AC-005)** — an honest, fully-fielded deferral is not named.
6. **Driven on the real CLI** — the full lifecycle walked end to end, the defect reproduced
   (`shipReady`, `observed: 0`), then refused under the flag, then shipped green against a real TRX.

## Registry obligation (cross-repo, FR-008)

ADR-0035 ties the `governance-handoff` minor bump to the **disposition** change — which is why #415
correctly did not bump it, and why this feature does. `registry/dependencies.yml` and
`docs/architecture.md` §5 live in `FS-GG/.github`; a PR here cannot touch them. Filed cross-repo and
referenced from the PR. It must land before the default flips.
