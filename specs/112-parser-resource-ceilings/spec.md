# Feature 112 — Authored-parser resource ceilings

**Tier:** 2 (test-only operational contract; no public API, schema, or output change)

## Outcome

The permanent adversarial YAML fixtures guard not only against process aborts, but also against
unbounded parser work. A representative document at the supported size ceiling has explicit,
reviewable elapsed-time and allocation budgets.

## Requirements

- **FR-001:** Keep the existing deeply nested flow, compact block-sequence, flat-sequence, and
  over-sized fixtures as permanent regression tests.
- **FR-002:** Parse a valid authored evidence document whose length is exactly the supported
  2,000,000-character ceiling and assert that it remains accepted.
- **FR-003:** After warming the parser path, assert explicit upper bounds for elapsed time and
  current-thread managed allocation while parsing that maximum-size document.
- **FR-004:** Choose ceilings with enough CI headroom to be stable while still detecting an
  accidental super-linear or multi-copy regression.

## Acceptance scenarios

1. A valid 2,000,000-character evidence document parses successfully within both budgets.
2. The existing over-depth and over-size documents still return ordinary diagnostics rather than
   aborting or reaching the YAML parser unbounded.

## Non-goals

- Changing the existing nesting or document-size limits.
- Introducing a benchmark framework or machine-specific throughput target.
- Changing any public signature, diagnostic, schema, or serialized artifact.
