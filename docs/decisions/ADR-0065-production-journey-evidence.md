# ADR-0065: Production journeys are a distinct evidence level

## Decision

Keep `{gameplay}` for deterministic simulation/component invariants and add
`{production-journey}` for requirements promising user-visible boot, reachability, progression, or
terminal behavior.

The latter mints a `production-journey` obligation. It is satisfied only by a successful schema-v1
Game journey receipt bound to the same passing observed test report. SDD derives provenance from the
receipt fields; `synthetic: false`, a green test name, a constructed playable, or any authored
provenance token is insufficient.

## Rationale

Simulation evidence is useful and remains honest evidence for lower-level invariants, but it cannot
establish entry through production composition seams. A separate typed level preserves that
distinction without changing existing work items.

## Consequences

Unknown versions and incomplete or inconsistent receipts fail closed. Journey totals and unmet
counts remain separate through verify, ship, and Governance handoff so downstream policy can adopt
the stronger floor independently.
