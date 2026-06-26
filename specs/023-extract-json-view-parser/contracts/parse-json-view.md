# Internal Contract: `parseJsonView`

**Visibility**: internal — defined in `[<AutoOpen>] module internal Internal`
(`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`), which has **no `.fsi`
signature file**. This contract is therefore an *internal* engineering contract,
not a public surface contract. No `.fsi` file changes as a result of this
feature, and no surface-area baseline is affected (FR-007, Constitution
Principle III).

## Signature

```fsharp
val parseJsonView:
    label: string ->
    malformedJsonCorrection: string ->
    build: (ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>) ->
    snapshot: FileSnapshot ->
        Result<'view, Diagnostic list>
```

## Behavior (reference implementation skeleton)

```fsharp
let parseJsonView
    (label: string)
    (malformedJsonCorrection: string)
    (build: ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>)
    (snapshot: FileSnapshot)
    : Result<'view, Diagnostic list> =
    let artifact = sourceArtifact snapshot.Path ArtifactKind.GeneratedView

    try
        use document = JsonDocument.Parse snapshot.Text
        let root = document.RootElement
        let rawVersion = jsonInt "schemaVersion" root |> Option.map string
        let compatibility = SchemaVersion.classifyRaw rawVersion

        match compatibility.Version, compatibility.Status with
        | Some schema, SchemaCompatibilityStatus.Current
        | Some schema, SchemaCompatibilityStatus.Deprecated -> build artifact schema root
        | _, SchemaCompatibilityStatus.Malformed
        | None, SchemaCompatibilityStatus.Current
        | None, SchemaCompatibilityStatus.Deprecated ->
            Error [ Diagnostics.malformedSchemaVersion artifact $"{label} is missing or has malformed schemaVersion." ]
        | _, SchemaCompatibilityStatus.Unsupported ->
            Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
        | _, SchemaCompatibilityStatus.Future ->
            Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
    with ex ->
        Error
            [ Diagnostics.workModelInconsistent
                  artifact
                  $"{label} JSON is malformed: {ex.Message}"
                  malformedJsonCorrection
                  [ snapshot.Path ] ]
```

> The exact arm grouping/ordering of the totality fix is an implementation
> detail; the binding requirements are: (a) the match is **total** (no FS0025),
> (b) `(None, Current/Deprecated)` and `Malformed` both yield the malformed-
> schema diagnostic, and (c) the produced diagnostics are byte-identical to the
> pre-refactor parsers for every already-handled state.

## Contract obligations

| ID | Obligation |
|---|---|
| C-1 | The `version, status` match is exhaustive; `dotnet build` emits 0 FS0025 for this code (FR-003, FR-005). |
| C-2 | On `Some schema, Current/Deprecated`, the result is exactly `build artifact schema root`. |
| C-3 | On `None, Current/Deprecated` **or** any `Malformed`, the result is `Error [ malformedSchemaVersion artifact "{label} is missing or has malformed schemaVersion." ]` (FR-004). |
| C-4 | On `Unsupported`/`Future`, the result is the existing unsupported/future diagnostic using `rawVersion |> Option.defaultValue ""` (unchanged). |
| C-5 | A `JsonDocument.Parse` (or downstream) throw is caught and surfaced as the existing `workModelInconsistent` diagnostic with message `"{label} JSON is malformed: {ex.Message}"`, correction `malformedJsonCorrection`, related id `[snapshot.Path]` — never propagated (Edge Cases, unchanged). |
| C-6 | The function is the **only** place the parse → classify → schema-error-arm → catch skeleton is expressed; no parser retains a copied skeleton body (FR-001, FR-007, SC-003). |

## Call-site contract (each of the four parsers)

```fsharp
let parseAnalysisView (snapshot: FileSnapshot) =
    parseJsonView
        "Analysis view"
        "Regenerate readiness/<id>/analysis.json with valid JSON."
        (fun artifact schema root ->
            // existing identity match + AnalysisView record build, verbatim
            ...)
        snapshot
```

Required `(label, malformedJsonCorrection)` pairs (must reproduce today's strings
exactly — SC-004):

| Entrypoint | `label` | `malformedJsonCorrection` |
|---|---|---|
| `parseAnalysisView` | `"Analysis view"` | `"Regenerate readiness/<id>/analysis.json with valid JSON."` |
| `parseVerificationView` | `"Verification view"` | `"Regenerate readiness/<id>/verify.json with valid JSON."` |
| `parseShipView` | `"Ship view"` | `"Regenerate readiness/<id>/ship.json with valid JSON."` |
| `parseGeneratedAgentGuidance` | `"Generated agent guidance"` | `"Regenerate the generated agent-commands guidance.json with valid JSON."` |

Each parser's public `.fsi` (`val parse… : FileSnapshot -> Result<…, Diagnostic list>`)
is unchanged.
