# Contract: `ArtifactCodec` interface (`.fsi` sketch)

The public surface introduced by this feature. Declared in
`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/ArtifactCodec.fsi` **before** the
`.fs` body (Principle I / III). Names are indicative; the authored `.fsi` in
implementation is the source of truth.

```fsharp
namespace FS.GG.SDD.Artifacts.LifecycleArtifacts

/// A field-list-driven codec: one declaration per artifact key binds its
/// reader and writer so read/write asymmetry is unrepresentable (FS.GG.SDD#201).
module ArtifactCodec =

    /// One field's contribution to parse (fold) and render (map) over a model 'M.
    /// A field absent from an artifact's `fields` list is neither read nor written.
    [<NoEquality; NoComparison>]
    type FieldCodec<'M> =
        { Key   : string
          Read  : YamlDotNet.RepresentationModel.YamlMappingNode -> 'M -> Result<'M, string>
          Write : 'M -> string option }

    /// Read null-aware (bare null/~/Null/NULL/empty -> None), write omit-when-None.
    /// Backed by Internal.isPlainNullScalar. The default for every 'a option field.
    val optionalScalar :
        key: string -> get: ('M -> string option) -> set: (string option -> 'M -> 'M) -> FieldCodec<'M>

    /// Read required (Error if absent), always write.
    val requiredScalar :
        key: string -> get: ('M -> string) -> set: (string -> 'M -> 'M) -> FieldCodec<'M>

    val inlineList  : key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>
    val scalarBlock : key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>

    /// Fold every field's reader over the parsed YAML map.
    val parse  : fields: FieldCodec<'M> list -> seed: 'M -> YamlDotNet.RepresentationModel.YamlMappingNode -> Result<'M, string>

    /// Map every field's writer; None-writers are omitted. Deterministic key order = list order.
    val render : fields: FieldCodec<'M> list -> model: 'M -> string
```

## Contract obligations

- **C-1 (symmetry)**: for a `fields` list and any model `m` reachable by parsing,
  `parse fields seed (yaml (render fields m)) = Ok m` over the authored fields.
- **C-2 (omission)**: `optionalScalar` with `get m = None` contributes no line to
  `render`.
- **C-3 (null-as-absence)**: `optionalScalar.Read` on a bare-null YAML scalar
  yields the model with `set None`; on a quoted `"null"` yields `set (Some "null")`.
- **C-4 (determinism)**: `render` output order is the `fields` list order; no
  clock, GUID, or environment read (Principle V, existing determinism contract).
- **C-5 (no `src/` reflection)**: `ArtifactCodec` uses no reflection or SRTP; the
  record↔codec coupling check that does use reflection lives in the test assembly
  only (FR-007, Principle IV).

## Consumers (refactored onto the codec, Phase 2 — Blocked by #189)

- `Evidence.parseEvidenceArtifact` / `HandlersEvidence.evidenceArtifactText`
  → one evidence `fields` list.
- `Task.parseTaskFrontMatter`/`parseTask*` / `TaskGraphAuthoring.*Text`
  → one tasks `fields` list.
- `HandlersVerify` reuses `optionalScalar`'s null-aware read for the
  synthetic-disclosure gate (parity with `evidence`).
