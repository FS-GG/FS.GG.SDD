# Requirements Quality Checklist — 092 Committed Compact Ship Verdict

Reviewed: 2026-07-08 · Source: [spec.md](../spec.md)

## Coverage (FR → AC)

- FR-001: `ship` emits the verdict beside `ship.json` (covers AC-US1-1)
- FR-002: exactly the eleven projected facts, no others (covers AC-US1-2)
- FR-003: `sourcesDigest` binds path→digest over the canonical pre-image (covers AC-US1-2)
- FR-004: ≤ 20 lines when no blocking finding ids (covers AC-US1-3)
- FR-005: no `ship.json` ⇒ no verdict (covers AC-US1-4)
- FR-006: `refresh` re-projects only when `ship.json` is already-current (covers AC-US3-1, AC-US3-2)
- FR-007: one shared pure projection for both producers (covers AC-US3-3)
- FR-008: byte-stable (covers AC-US3-4)
- FR-009: seeded `.gitignore` contents rule + negation (covers AC-US2-1, AC-US2-2)
- FR-010: this repo's dogfood rule (covers AC-US2-3)
- FR-011: byte-exact seed + doc-fragment guards still hold (covers AC-US2-1)
- FR-012: catalog entry with `durableGenerated: true`; others `false` (covers AC-US4-3, AC-US4-4)
- FR-013: taxonomy tables are catalog-derived partitions (covers AC-US4-1, AC-US4-2)
- FR-014: behavioral git test for the seeded rule (covers AC-US2-1, AC-US2-2)
- FR-015: behavioral git test for the dogfood rule + root proofs (covers AC-US2-3)
- FR-016: view-kind guard amended; covers-every-kind guard still holds (covers AC-US4-3)
- FR-017: `validate` enumerates the view (covers AC-US3-4)
- FR-018: no `ship.json` change; no cross-repo contract (covers AC-US1-2)
- FR-019: additive, no-clobber adoption (covers AC-US2-4)

## Quality gates

| Check | Verdict |
|---|---|
| Every FR is testable and observable from outside the implementation | **Pass** — each maps to a file on disk, a git index state, a report field, or a guard |
| No FR restates an implementation detail as a requirement | **Pass** — FR-007 names the *property* (identical bytes from one projection), the plan names the function |
| Load-bearing claims are verified, not assumed | **Pass** — research D1 (git), D2 (vacuous assert), D3/D4 (guards read), D7/D9 (code read), D8 (digest recomputed) |
| Ambiguities resolved before planning | **Pass** — eleven recorded in spec Clarifications; the three that changed the design are D1/D3/D5 |
| Success criteria are measurable, not aspirational | **Pass** — SC-001..SC-013 each name a command, a count, or a byte comparison |
| Negative/regression cases specified | **Pass** — SC-004 asserts the pre-feature rule stages *nothing*; SC-008 blocked ship; SC-013 non-adopting repo |
| Out-of-scope stated explicitly | **Pass** — `verify.json`, `governance-handoff.json`, `doctor` 0018-era check, history rewrite (FR-018, spec Assumptions) |
| Cross-repo impact assessed | **Pass** — none: no `contractVersion`, no `registry/dependencies.yml` entry; consumers adopt additively |

## Risks accepted

- **T024 amendment**: feature 018's guard is deliberately widened. Mitigated by T019 forcing the
  catalog entry, so the pair cannot be half-satisfied.
- **`PublicSurface.baseline` moves** for `FS.GG.SDD.Artifacts` (new module + two additive fields).
  Regenerated deliberately with `FSGG_UPDATE_BASELINE=1` and reviewed in the diff.
- **The `.gitignore` seed is no-clobber**, so existing consumer repos do not receive the amended
  fragment automatically. Accepted and documented (FR-019, ADR-0026 Consequences); adoption is by
  hand from `docs/reference/artifact-taxonomy.md`, exactly as ADR-0018 prescribed.
