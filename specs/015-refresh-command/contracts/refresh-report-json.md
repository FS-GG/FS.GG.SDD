# Contract: Refresh Command Report JSON

**Status**: Tier 1 contract | **Schema version**: 1 | **Command**: `refresh`

The refresh report reuses the shared `CommandReport` envelope serialized by
`CommandSerialization`. This document specifies the refresh-specific population
of that envelope. The JSON report is authoritative; the text projection and
`summary.md` add no facts beyond it (FR-019, FR-006).

## Envelope (reused `CommandReport` fields)

```jsonc
{
  "schemaVersion": 1,
  "reportVersion": "<report-version>",
  "command": "refresh",
  "projectRoot": "<root>",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "allowGeneratedRefresh",
  "outcome": "succeeded | succeededWithWarnings | blocked | noChange",
  "workId": "<id>",
  "changedArtifacts": [ /* ArtifactChange: refreshed generated views + preserved authored sources */ ],
  "refresh": { /* RefreshSummary, see below */ },
  "generatedViews": [ /* GeneratedViewState per SDD-owned view */ ],
  "diagnostics": [ /* Diagnostic[] */ ],
  "governanceCompatibility": [ /* advisory facts only */ ],
  "nextAction": { /* NextAction */ }
}
```

Only the `refresh` summary block is new; all other fields already exist and are
serialized by the shared serializer.

## `refresh` block (`RefreshSummary`)

```jsonc
{
  "workId": "<id>",
  "stage": "refresh",
  "status": "<status>",
  "summaryPath": "readiness/<id>/summary.md",
  "refreshedViewIds": ["work-model", "analysis", "verify", "ship", "summary"],
  "alreadyCurrentViewIds": ["agent-commands"],
  "blockedViewIds": [],
  "notApplicableViewIds": [],
  "preservedAuthoredPaths": ["work/<id>/spec.md", "..."],
  "findingIds": ["..."],
  "advisoryCount": 0,
  "warningCount": 0,
  "blockingCount": 0,
  "disposition": "refreshed-current | partially-blocked | blocked",
  "perViewState": [
    ["work-model", "current"],
    ["analysis", "current"],
    ["verify", "current"],
    ["ship", "current"],
    ["agent-commands", "current"],
    ["summary", "current"]
  ],
  "sourceSnapshotCount": 5,
  "readiness": "<readiness-string>"
}
```

## `generatedViews[]` (`GeneratedViewState`)

One entry per SDD-owned view, each recording `path`, `kind`, `schemaVersion`,
`generator`, `sources[]` (path, digest, schemaVersion, schemaStatus),
`outputDigest`, `currency` (`current|missing|stale|malformed|blocked`), and
`diagnosticIds` (FR-007, FR-008, FR-009).

## Diagnostics

Each diagnostic carries a stable `id`, `severity`, affected `path` (the view) and
related ids (the affected source or upstream view when available), `message`, and
a user-correctable `correction` (FR-020). Refresh-specific ids include
`refreshMissingSource`, `refreshMalformedSource`, `refreshStaleView`,
`refreshMalformedGeneratedView`, `refreshBlockedUpstreamView`, and
`refreshUnrenderableSummary`, plus reused shared ids (`outsideProject`,
`malformedWorkId`, `duplicateWorkId`, `unknownSourceReference`,
`malformedGeneratedView`, `blockedGeneratedViewRefresh`).

## Determinism (FR-018, SC-004)

- Stable key order, stable array order (declared-source / view-kind order, not
  filesystem enumeration order).
- No clocks, durations, ANSI, terminal width, host path separators, randomness,
  or absolute host paths.
- Three identical refreshes over identical inputs produce byte-identical JSON and
  byte-identical regenerated views.

## Governance compatibility (FR-023)

`governanceCompatibility[]` may expose Governance-compatible pointers as advisory
facts (`path`, `relationship`, `requiredBySdd: false`, `state`, `diagnosticIds`).
SDD never interprets them as freshness/route/profile/gate/audit/release/boundary
decisions, including stale-view blocking at a protected boundary.
