# Phase 1 Data Model: Extract a shared JSON view-parser skeleton

This refactor introduces **no new data types** and changes **no existing record,
DU, or schema**. The four view record types (`AnalysisView`, `VerificationView`,
`ShipView`, `GeneratedAgentGuidance`) and all their nested records keep their
exact shapes, field orders, and parsers. The only "entity" introduced is the
internal skeleton function and the shape of its `build` callback — captured here
because it is the contract between the skeleton and each parser.

## Existing types referenced (unchanged)

| Type | Module | Role in this feature |
|---|---|---|
| `FileSnapshot` | `GenerationManifest` | Input to every parser (`{ Path; Text }`). |
| `SchemaCompatibility` | `SchemaVersion` | `{ RawValue; Version: SchemaVersion option; Status; … }` — produced by `classifyRaw`; the skeleton matches on `Version, Status`. |
| `SchemaCompatibilityStatus` | `SchemaVersion` | DU `Current │ Deprecated │ Unsupported │ Malformed │ Future` — the status axis of the total match. |
| `SchemaVersion` | `SchemaVersion` | `{ Major; Minor; Raw }` — the `schema` value handed to `build` on the success arm. |
| `ArtifactRef` | `ArtifactRef` | The `sourceArtifact snapshot.Path GeneratedView` value; passed to `build` and used by every error arm. |
| `Diagnostic` | `Diagnostics` | Error payload element; `Result<'view, Diagnostic list>` is every parser's return type. |

## New internal contract: `parseJsonView`

A single generic helper added to `module internal Internal`. Not a data type —
the "model" is its parameter contract.

```fsharp
val parseJsonView:
    label: string ->                                  // prose noun for schema/JSON error messages
    malformedJsonCorrection: string ->                // catch-arm correction (per-artifact path)
    build: (ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>) ->
    snapshot: FileSnapshot ->
        Result<'view, Diagnostic list>
```

### Field/parameter semantics

| Parameter | Type | Meaning | Source of truth |
|---|---|---|---|
| `label` | `string` | Noun phrase for the artifact, e.g. `"Analysis view"`. Used in the Malformed-arm message and the catch message. | Each parser, verbatim from its current strings. |
| `malformedJsonCorrection` | `string` | Correction text for the `try/with` malformed-JSON diagnostic. | Each parser, verbatim. |
| `build` | callback | Given the resolved `artifact`, the parsed `schema`, and the JSON `root`, validate identity fields and either `Ok view` or `Error [identity diagnostic]`. Invoked **only** on the `Some schema, Current/Deprecated` arm. | Each parser's existing success body. |
| `snapshot` | `FileSnapshot` | The raw artifact text + path. | Caller (unchanged public entrypoint). |
| (result) | `Result<'view, Diagnostic list>` | Identical to each parser's current return type. | — |

### Control-flow states (the total match)

The skeleton's `version, status` decision is **total**. Outcome table over
`Version ∈ {Some, None}` × `Status ∈ {Current, Deprecated, Unsupported, Malformed, Future}`:

| Version | Status | Outcome |
|---|---|---|
| `Some schema` | `Current` / `Deprecated` | `build artifact schema root` (happy path → identity validation) |
| `None` | `Current` / `Deprecated` | `Error [ malformedSchemaVersion artifact "{label} is missing or has malformed schemaVersion." ]` — **new total arm** (was an unhandled `MatchFailureException`) |
| any | `Malformed` | `Error [ malformedSchemaVersion artifact "{label} is missing or has malformed schemaVersion." ]` (unchanged behavior) |
| any | `Unsupported` | `Error [ unsupportedSchemaVersion artifact rawVersion ]` (unchanged) |
| any | `Future` | `Error [ futureSchemaVersion artifact rawVersion ]` (unchanged) |
| (parse throws) | — | `try/with` → `Error [ workModelInconsistent artifact "{label} JSON is malformed: {ex.Message}" malformedJsonCorrection [snapshot.Path] ]` (unchanged) |

`None, Current` / `None, Deprecated` collapse into the same arm as `Malformed`,
so the malformed-schema diagnostic is constructed in exactly one place (FR-002,
FR-004). All 10 (version × status) combinations are covered → no FS0025.

## Per-parser `build` callbacks (unchanged success bodies, relocated)

Each `build` is the current parser's success body, with its identity-error arm,
verbatim. Identity validation differs by artifact:

| Parser | Identity validation inside `build` | View record produced |
|---|---|---|
| Analysis | `createWorkId workId`, `parseStage stage` | `AnalysisView` |
| Verify | `createWorkId workId`, `parseStage stage` | `VerificationView` |
| Ship | `createWorkId workId`, `parseStage stage` | `ShipView` |
| Guidance | `createWorkId workId`, `jsonDigest "behaviorModelDigest"`, non-empty `targetId` | `GeneratedAgentGuidance` |

No view record fields, defaults, sort orders, or field parsers change — only the
enclosing skeleton is hoisted out of each body.
