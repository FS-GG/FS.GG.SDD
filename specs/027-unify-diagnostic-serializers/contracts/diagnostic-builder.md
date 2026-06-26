# Internal Contract: Command Diagnostic Builder Collapse

Story 1. All new builders are **internal-only** (absent from `CommandReports.fsi`).
The public surface — the existing `commandDiagnostic` plus the ~113 named
constructors — is held byte-identical.

## Public surface (UNCHANGED — byte-stable)

```fsharp
// CommandReports.fsi — unchanged, every line byte-identical to baseline
val commandDiagnostic:
    id: string -> severity: DiagnosticSeverity -> path: string option ->
    message: string -> correction: string -> relatedIds: string list -> Diagnostic
// … all ~113 named constructors keep their exact signatures …
```

## Internal builders (NOT in `.fsi`) — sketch

```fsharp
// error-default: removes ~99 hand-spelled DiagnosticError literals
let errorDiagnostic id path message correction relatedIds =
    commandDiagnostic id DiagnosticSeverity.DiagnosticError path message correction relatedIds

// warning-default: the 14 warning constructors route here (no error promotion)
let warningDiagnostic id path message correction relatedIds =
    commandDiagnostic id DiagnosticSeverity.DiagnosticWarning path message correction relatedIds

// family helpers capture each family's shared id/path/message/correction skeleton;
// only the varying pieces are parameters. Examples (final shapes set in impl):
//   missing*    : required-artifact-missing shape
//   malformed*  : malformed-artifact shape
//   duplicate*  : duplicate-id shape
//   unknown*    : unknown-reference shape
//   stale*      : stale-view/result shape (severity varies: some warning)
//   unsafe* / failed* : unsafe-change / failed-gate shape
```

## Invariants (maps to spec)

- **SC-001**: 100% of command diagnostics route through `commandDiagnostic` via a
  default/family helper; zero inline severity/path/sort handling remains.
- **FR-002**: error is the builder default; the **14** warning constructors keep
  `DiagnosticWarning`. The 14 are enumerated from source — among them:
  `failedRequirementsQuality`, `staleChecklistResult`, `stalePlanDecision`,
  `staleTask`, `malformedAnalysisView`, `staleEvidence`, `staleEvidenceSource`,
  `malformedGeneratedView`, `blockedGeneratedViewRefresh`, `staleRequiredTest`,
  `agentsStaleGeneratedGuidance`, `refreshStaleView`,
  `refreshMalformedGeneratedView` (+ the remaining to reach 14, re-confirmed at
  impl time via `grep -c DiagnosticWarning` = 14). No flip in either direction.
- **FR-003**: families share the skeleton through helpers without merging or
  renaming any named function.
- **FR-009 / SC-004**: every emitted id, severity, path, message, correction,
  related-id, and sort order is byte-identical to baseline in `--json`.
- **FR-007 / SC-005**: `CommandReports.fsi` and the Commands `PublicSurface.baseline`
  are byte-identical to baseline.

## Independent test (from spec User Story 1)

1. Search confirms every command diagnostic is built through the one builder.
2. A diagnostic-emitting command (e.g. missing-prerequisite, duplicate-id) yields
   a `diagnostics` array byte-identical to baseline.
3. `CommandReports.fsi` diff vs baseline is empty.
