# Contract: Refresh Fixture Corpus

Fixtures live under `tests/fixtures/lifecycle-commands/`. Refresh adds new roots
for orchestrated and summary-specific scenarios and reuses the established shared
roots with refresh-specific expected outputs. Each fixture provides the input
project tree plus the expected JSON report (and, where relevant, expected
refreshed views and `summary.md`) for golden comparison.

## Valid refresh families (SC-002)

| Fixture root | Scenario | Key assertions |
|---|---|---|
| `refresh-current` | all views already current | `NoChange`/`refreshed-current`; zero writes; all `perViewState = current` |
| `refresh-stale-views` | some sources changed since views produced | affected views refreshed, others reported already-current (US1-2, US2-1) |
| `refresh-missing-view` | a generated view file absent | view regenerated for first time; `currency` was `missing` → `current` |
| `refresh-summary` | `summary.md` rendered from structured data | summary marked generated, records sources, matches report facts (US3, SC-008) |
| `refresh-preserves-authored` | authored sources + `.fsgg/*.yml` present | authored bytes unchanged; `Preserve`/`NoChange` operations (US4-1, FR-012) |
| `refresh-no-agent-targets` | no agent config / no targets | `agent-commands` reported `not-applicable`, not blocked |
| `dry-run` (reused) | `--dry-run` over a stale project | proposed changes + diagnostics reported; zero files changed (FR-021, SC-006) |
| `deterministic-report` (reused) | repeated identical refresh | byte-identical views + JSON across 3 runs (FR-018, SC-004) |
| `text-projection` (reused) | text output equals JSON facts | text adds no facts (FR-019) |
| `governance-boundary` (reused) | optional Governance pointers, no Governance installed | advisory facts only; refresh succeeds without Governance (FR-022–FR-024, SC-009) |

## Blocked refresh families (SC-003)

Each leaves authored content unchanged and includes ≥1 actionable diagnostic.

| Fixture root | Scenario | Diagnostic |
|---|---|---|
| `outside-project` (reused) | run before `fsgg-sdd init` | `outsideProject` |
| `malformed-work-id` (reused) | empty/malformed selected id | `malformedWorkId` |
| `duplicate-work-id` (reused) | duplicated logical work id | `duplicateWorkId` |
| `missing-source` (new) | a view's declared source absent | `refreshMissingSource` |
| `malformed-source` (new) | a view's source malformed/schema-incompatible | `refreshMalformedSource` |
| `stale-source` | source changed, view cannot be safely refreshed | `refreshStaleView` |
| `unknown-source-reference` (reused) | source references an unknown id | `unknownSourceReference` |
| `malformed-generated-view` | existing generated view unreadable | `refreshMalformedGeneratedView` |
| `blocked-upstream-view` | dependent view blocked on un-current upstream | `refreshBlockedUpstreamView` (names upstream) |

## Summary faithfulness fixtures (SC-008)

`refresh-summary` (and the diagnostic variant within `blocked-upstream-view`)
assert that `summary.md`'s per-view state table, diagnostics, outcome, and next
action equal the report's `refresh.perViewState`, `diagnostics`, `outcome`, and
`nextAction`, and that the summary contains no fact absent from the structured
views. The unrenderable-summary case asserts `refreshUnrenderableSummary` and a
`Blocked` summary view with no `summary.md` written from unusable data.

## Determinism harness

`deterministic-report` runs refresh three times over identical inputs and asserts
byte-identical regenerated views and JSON reports (SC-004). All fixtures exclude
clocks, durations, ANSI, enumeration order, host path separators, randomness, and
absolute host paths.

## Coverage note

Per the precedent set by feature `014-agent-guidance`, scenarios may be covered
by real-evidence `RefreshCommandTests` exercising temporary project trees instead
of static fixture directories where that yields stronger evidence; any deferral
of a static fixture root is disclosed in `tasks.md`.
