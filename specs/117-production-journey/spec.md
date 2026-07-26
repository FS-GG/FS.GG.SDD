# Feature 117: Production-journey evidence

## Problem

An observed test report proves that a runner executed, but does not prove that a user-visible game
journey entered through the producer-owned production composition. A helper or component test must
not satisfy a requirement that promises boot, progression, reachability, or a terminal outcome.

## Requirements

- FR-001 `{production-journey}`: A classified requirement receives one distinct production-journey
  obligation. Existing unclassified and `{gameplay}` requirements retain their current semantics.
- FR-002: A production-journey obligation is satisfied only by a non-synthetic verification pass
  carrying a complete Game journey receipt and a passing observed-run receipt.
- FR-003: SDD validates receipt schema 1, runner identity/version, production origin, route/scenario/
  test identities, input kind/digest, replay and trace digests, initial/terminal fingerprints,
  terminal predicate, passed outcome, positive bounded steps, and exact observed-report binding.
- FR-004: Missing, malformed, unknown-version, simulation-origin, failed, exhausted, mismatched, or
  hand-authored substitute provenance fails closed with a stable diagnostic.
- FR-005: Evidence, verify, ship, and Governance handoff preserve journey totals and unmet counts
  separately from ordinary gameplay obligations.

## Compatibility

The change is additive while `{production-journey}` is unused. No authored token can substitute for
the machine-issued `journeyReceipt` map.

## Performance intent

This is offline artifact parsing and lifecycle validation. It does not change a render/update hot
path, a representative game workload, or a live-compositor requirement; therefore no product
performance intent or timing budget is introduced.
