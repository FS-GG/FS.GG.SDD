# Contract: Checklist Command Report JSON

## Scope

Checklist reports follow the existing native command report JSON contract and
add checklist-specific expectations for changed artifacts, parsed facts,
generated views, diagnostics, and next action. JSON remains the authoritative
result; text output is a projection from the same report object.

## Successful Checklist-Ready Example

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0.0",
  "command": {
    "name": "checklist",
    "stage": "checklist"
  },
  "context": {
    "projectRoot": ".",
    "workId": "007-checklist-command"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeeded",
  "changedArtifacts": [
    {
      "path": "work/007-checklist-command/checklist.md",
      "kind": "authoredSource",
      "ownership": "authored",
      "operation": "create",
      "beforeDigest": null,
      "afterDigest": {
        "algorithm": "sha256",
        "value": "<lowercase-sha256>"
      },
      "safeWriteDecision": "safe",
      "diagnosticIds": []
    }
  ],
  "specification": {
    "workId": "007-checklist-command",
    "stage": "specify",
    "status": "specified",
    "storyIds": [
      "US-001"
    ],
    "requirementIds": [
      "FR-001"
    ],
    "acceptanceScenarioIds": [
      "AC-001"
    ],
    "ambiguityIds": []
  },
  "clarification": {
    "workId": "007-checklist-command",
    "stage": "clarify",
    "status": "clarified",
    "sourceSpec": "work/007-checklist-command/spec.md",
    "questionIds": [],
    "answeredQuestionIds": [],
    "decisionIds": [
      "DEC-001"
    ],
    "acceptedDeferralIds": [],
    "remainingAmbiguityCount": 0,
    "blockingAmbiguityCount": 0
  },
  "checklist": {
    "workId": "007-checklist-command",
    "stage": "checklist",
    "status": "checklistReady",
    "sourceSpec": "work/007-checklist-command/spec.md",
    "sourceClarifications": "work/007-checklist-command/clarifications.md",
    "itemIds": [
      "CHK-001"
    ],
    "resultIds": [
      "CR-001"
    ],
    "passedCount": 1,
    "failedBlockingCount": 0,
    "acceptedDeferralCount": 0,
    "staleResultCount": 0,
    "advisoryCount": 0
  },
  "generatedViews": [
    {
      "path": "readiness/007-checklist-command/work-model.json",
      "kind": "workModel",
      "schemaVersion": 1,
      "generator": {
        "id": "FS.GG.SDD.Commands",
        "version": "0.1.0"
      },
      "sources": [
        {
          "path": "work/007-checklist-command/spec.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        },
        {
          "path": "work/007-checklist-command/clarifications.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        },
        {
          "path": "work/007-checklist-command/checklist.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        }
      ],
      "outputDigest": null,
      "currency": "missing",
      "diagnosticIds": []
    }
  ],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": {
    "actionId": "nextLifecycleCommand",
    "command": "plan",
    "workId": "007-checklist-command",
    "reason": "Command 'checklist' completed.",
    "requiredArtifacts": [
      "work/007-checklist-command/spec.md",
      "work/007-checklist-command/clarifications.md",
      "work/007-checklist-command/checklist.md"
    ],
    "blockingDiagnosticIds": []
  }
}
```

The example uses placeholders only where digest values are implementation
outputs. If the existing command report type cannot include a nested
`checklist` object without a broader public API change, the same facts must be
represented through documented report fields, generated-view sources, and
diagnostics before the feature is accepted.

## Failed Quality Example

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0.0",
  "command": {
    "name": "checklist",
    "stage": "checklist"
  },
  "context": {
    "projectRoot": ".",
    "workId": "007-checklist-command"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeededWithWarnings",
  "changedArtifacts": [
    {
      "path": "work/007-checklist-command/checklist.md",
      "kind": "authoredSource",
      "ownership": "authored",
      "operation": "create",
      "beforeDigest": null,
      "afterDigest": {
        "algorithm": "sha256",
        "value": "<lowercase-sha256>"
      },
      "safeWriteDecision": "safe",
      "diagnosticIds": [
        "failedRequirementsQuality"
      ]
    }
  ],
  "checklist": {
    "workId": "007-checklist-command",
    "stage": "checklist",
    "status": "needsCorrection",
    "sourceSpec": "work/007-checklist-command/spec.md",
    "sourceClarifications": "work/007-checklist-command/clarifications.md",
    "itemIds": [
      "CHK-001"
    ],
    "resultIds": [
      "CR-001"
    ],
    "passedCount": 0,
    "failedBlockingCount": 1,
    "acceptedDeferralCount": 0,
    "staleResultCount": 0,
    "advisoryCount": 0
  },
  "diagnostics": [
    {
      "id": "failedRequirementsQuality",
      "severity": "error",
      "artifact": "work/007-checklist-command/spec.md",
      "message": "Requirement FR-001 is missing acceptance coverage.",
      "correction": "Add an acceptance scenario for FR-001 or narrow the requirement.",
      "relatedIds": [
        "FR-001",
        "CHK-001",
        "CR-001"
      ]
    }
  ],
  "governanceCompatibility": [],
  "nextAction": {
    "actionId": "correctBlockingDiagnostics",
    "command": null,
    "workId": "007-checklist-command",
    "reason": "Checklist has blocking requirements-quality findings.",
    "requiredArtifacts": [
      "work/007-checklist-command/spec.md",
      "work/007-checklist-command/clarifications.md",
      "work/007-checklist-command/checklist.md"
    ],
    "blockingDiagnosticIds": [
      "failedRequirementsQuality"
    ]
  }
}
```

Failed quality reports may include `specification`, `clarification`,
`generatedViews`, and other existing command report sections in the same order
as successful reports. The abbreviated example shows only the fields needed to
define the failed-quality contract.

## Required Ordering

The existing ordering contract still applies:

1. Object properties emit in documented order.
2. Artifact changes sort by path, operation, and ownership.
3. Specification ids sort by id family and numeric suffix.
4. Clarification ids sort by id family and numeric suffix.
5. Checklist item and result ids sort by id family and numeric suffix.
6. Generated views sort by path.
7. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
8. Governance compatibility facts sort by path.
9. Next-action required artifacts sort by path.

## Checklist-Specific Changed Artifacts

Required checklist artifact entry fields:

- `path`: `work/<id>/checklist.md`
- `kind`: `authoredSource` or the existing command write-kind value for
  authored sources
- `ownership`: `authored`
- `operation`: one of `create`, `update`, `preserve`, `refuse`, `noChange`
- `safeWriteDecision`: `safe`, `preserveExisting`, `refuseConflict`,
  `dryRunOnly`, or equivalent existing value with the same meaning

Validation:

- Refused checklist writes include diagnostic ids.
- Successful writes include `afterDigest`.
- Existing files include `beforeDigest`.
- Dry-run reports describe proposed changes but do not mutate disk.
- Failed requirements-quality writes may be safe authored writes when source
  facts are valid and no authored content is clobbered.

## Checklist-Specific Diagnostics

Required stable diagnostic coverage:

- `outsideProject` or an equivalent missing-project diagnostic
- `missingWorkId`
- `malformedWorkId`
- `missingSpecificationPrerequisite`
- `missingClarificationPrerequisite`
- `specificationIdentityMismatch`
- `clarificationIdentityMismatch`
- `unresolvedBlockingAmbiguity`
- `failedRequirementsQuality`
- `checklistIdentityMismatch`
- `malformedChecklistFrontMatter`
- `duplicateChecklistId`
- `unknownChecklistSourceReference`
- `staleChecklistResult`
- `unsafeChecklistResultChange`
- `staleGeneratedView`
- `malformedGeneratedView`
- `blockedGeneratedViewRefresh`
- optional Governance boundary issue diagnostics when applicable

Diagnostic entries must include artifact, severity, message, correction, and
related ids when available.

## Next Action

Successful checklist-ready reports set:

```json
{
  "actionId": "nextLifecycleCommand",
  "command": "plan",
  "workId": "<selected-work-id>",
  "reason": "Command 'checklist' completed.",
  "requiredArtifacts": [
    "work/<selected-work-id>/spec.md",
    "work/<selected-work-id>/clarifications.md",
    "work/<selected-work-id>/checklist.md"
  ],
  "blockingDiagnosticIds": []
}
```

Reports with failed blocking checks, stale results, malformed prerequisites,
or unsafe writes set `actionId` to `correctBlockingDiagnostics`, omit a next
lifecycle command, and include the blocking diagnostic ids. If the checklist
artifact is syntactically valid but results need review, the report must name
the failed, stale, accepted-deferral, and advisory counts so users understand
why plan is not next.

## Determinism Rules

- JSON uses UTF-8 without a byte order mark.
- Paths use `/`.
- Object property order follows the report contract.
- Lists use documented sort keys.
- Digests are lowercase SHA-256 hex over normalized bytes.
- Reports do not contain timestamps, durations, terminal width, ANSI styling,
  process ids, random ids, absolute host paths, or directory enumeration order.
- Text projection tests must prove text mode derives from this report without
  adding separate facts.
