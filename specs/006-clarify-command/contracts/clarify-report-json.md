# Contract: Clarify Command Report JSON

## Scope

Clarify reports follow the existing native command report JSON contract and add
clarification-specific expectations for changed artifacts, parsed facts,
generated views, diagnostics, and next action. JSON remains the authoritative
result; text output is a projection from the same report object.

## Successful Create Example

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0.0",
  "command": {
    "name": "clarify",
    "stage": "clarify"
  },
  "context": {
    "projectRoot": ".",
    "workId": "006-clarify-command"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeeded",
  "changedArtifacts": [
    {
      "path": "work/006-clarify-command/clarifications.md",
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
  "clarification": {
    "workId": "006-clarify-command",
    "stage": "clarify",
    "status": "clarified",
    "sourceSpec": "work/006-clarify-command/spec.md",
    "questionIds": [
      "CQ-001"
    ],
    "answeredQuestionIds": [
      "CQ-001"
    ],
    "decisionIds": [
      "DEC-001"
    ],
    "acceptedDeferralIds": [],
    "remainingAmbiguityCount": 0,
    "blockingAmbiguityCount": 0
  },
  "generatedViews": [
    {
      "path": "readiness/006-clarify-command/work-model.json",
      "kind": "workModel",
      "schemaVersion": 1,
      "generator": {
        "id": "FS.GG.SDD.Commands",
        "version": "0.1.0"
      },
      "sources": [
        {
          "path": "work/006-clarify-command/spec.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        },
        {
          "path": "work/006-clarify-command/clarifications.md",
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
    "command": "checklist",
    "workId": "006-clarify-command",
    "reason": "Command 'clarify' completed.",
    "requiredArtifacts": [
      "work/006-clarify-command/spec.md",
      "work/006-clarify-command/clarifications.md"
    ],
    "blockingDiagnosticIds": []
  }
}
```

The example uses placeholders only where digest values are implementation
outputs. If the existing command report type cannot include a nested
`clarification` object without a broader public API change, the same facts must
be represented through documented report fields, generated-view sources, and
diagnostics before the feature is accepted.

## Required Ordering

The existing ordering contract still applies:

1. Object properties emit in documented order.
2. Artifact changes sort by path, operation, and ownership.
3. Clarification ids sort by id family and numeric suffix.
4. Generated views sort by path.
5. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
6. Governance compatibility facts sort by path.
7. Next-action required artifacts sort by path.

## Clarify-Specific Changed Artifacts

Required clarification artifact entry fields:

- `path`: `work/<id>/clarifications.md`
- `kind`: `authoredSource` or the existing command write-kind value for
  authored sources
- `ownership`: `authored`
- `operation`: one of `create`, `update`, `preserve`, `refuse`, `noChange`
- `safeWriteDecision`: `safe`, `preserveExisting`, `refuseConflict`,
  `dryRunOnly`, or equivalent existing value with the same meaning

Validation:

- Refused clarification writes include diagnostic ids.
- Successful writes include `afterDigest`.
- Existing files include `beforeDigest`.
- Dry-run reports describe proposed changes but do not mutate disk.

## Clarify-Specific Diagnostics

Required stable diagnostic coverage:

- `outsideProject` or an equivalent missing-project diagnostic
- `missingWorkId`
- `malformedWorkId`
- `missingSpecificationPrerequisite`
- `specificationIdentityMismatch`
- `malformedSpecificationFacts`
- `missingClarificationAnswer`
- `clarificationIdentityMismatch`
- `malformedClarificationFrontMatter`
- `duplicateClarificationId`
- `unknownClarificationReference`
- `unsafeDecisionChange`
- `unresolvedBlockingAmbiguity`
- `staleGeneratedView`
- `malformedGeneratedView`
- `blockedGeneratedViewRefresh`
- optional Governance boundary issue diagnostics when applicable

Diagnostic entries must include artifact, severity, message, correction, and
related ids when available.

## Next Action

Successful clarify reports with no blocking ambiguity set:

```json
{
  "actionId": "nextLifecycleCommand",
  "command": "checklist",
  "workId": "<selected-work-id>",
  "reason": "Command 'clarify' completed.",
  "requiredArtifacts": [
    "work/<selected-work-id>/spec.md",
    "work/<selected-work-id>/clarifications.md"
  ],
  "blockingDiagnosticIds": []
}
```

Blocked reports set `actionId` to `correctBlockingDiagnostics`, omit a next
lifecycle command, and include the blocking diagnostic ids. If the
clarification artifact is syntactically valid but blocking ambiguity remains,
the result may point to additional clarification; the report must name the
remaining and blocking ambiguity counts so users understand why checklist is
not next.

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
