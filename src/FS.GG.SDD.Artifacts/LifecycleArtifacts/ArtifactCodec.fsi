namespace FS.GG.SDD.Artifacts

/// Field-list-driven codec for authored YAML artifacts (FS.GG.SDD#201, ADR-0002
/// invariant 1). One `fields` list per artifact drives BOTH decode and render,
/// so a field cannot be read without being written, or written without being
/// read — the read/write asymmetry behind #180/#181/#182 becomes unrepresentable.
///
/// Optional scalars read null-aware: a bare `null`/`Null`/`NULL`/`~`/empty YAML
/// token is absence (`None`); a quoted `"null"` is the string `Some "null"`.
/// `render` omits an absent optional (no empty-value line, no invented default).
///
/// This is the foundational module (Phase 2). The evidence/tasks parsers and
/// emitters are refactored onto it in the implementation slice, which is
/// Blocked by FS.GG.SDD#189 (touch-set overlap).
[<RequireQualifiedAccess>]
module ArtifactCodec =

    /// One field's binding of a YAML key to a reader (decode) and a writer
    /// (render) over a model `'M`. Opaque: construct via the helpers below so the
    /// two directions are always declared together.
    [<NoEquality; NoComparison>]
    type FieldCodec<'M>

    /// A `string option` field. Reads null-aware (bare null token -> `None`;
    /// quoted `"null"` -> `Some "null"`); writes `key: value`, or omits the line
    /// when `None`.
    val optionalScalar: key: string -> get: ('M -> string option) -> set: (string option -> 'M -> 'M) -> FieldCodec<'M>

    /// A required `string` field. Reads the key (Error if absent); always writes.
    val requiredScalar: key: string -> get: ('M -> string) -> set: (string -> 'M -> 'M) -> FieldCodec<'M>

    /// A `string` field with a fallback: reads the key, or `fallback` when the key is
    /// absent (never errors); always writes `key: value`. Mirrors the reader's
    /// `tryScalarAt |> Option.defaultValue` for keys such as evidence `sourceRef.kind`.
    val defaultedScalar:
        key: string -> fallback: string -> get: ('M -> string) -> set: (string -> 'M -> 'M) -> FieldCodec<'M>

    /// A `string list` rendered as a YAML flow sequence `key: [a, b, c]`. An
    /// empty list omits the line; absence reads as `[]`.
    val inlineList: key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>

    /// Like `inlineList` but for a fixed-shape record where the key is always present:
    /// an empty list renders `key: []` rather than omitting the line. Distinct+sorted on
    /// write (matching the legacy `yamlInlineList`); the reader preserves order.
    val alwaysInlineList: key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>

    /// A typed-id list (`key: [FR-001, FR-002]`), always present (`key: []` when empty),
    /// distinct+sorted on write. The read is lenient — parse each token via `create`, drop
    /// malformed, order preserved (mirroring `parseTaskIds`); the malformed-ref diagnostics
    /// remain the caller's semantic-layer responsibility, so nothing is silently lost.
    val refList:
        key: string ->
        create: (string -> Result<'id, 'e>) ->
        value: ('id -> string) ->
        get: ('M -> 'id list) ->
        set: ('id list -> 'M -> 'M) ->
            FieldCodec<'M>

    /// A scalar mapped through a total render/read pair — `toStr` on write, `ofStr` on read
    /// (which must be total, supplying a default for an unrecognised token like the lenient
    /// `parseEvidenceKind`). An absent key keeps the seed value. Always writes.
    val mappedScalar:
        key: string ->
        toStr: ('a -> string) ->
        ofStr: (string -> 'a) ->
        get: ('M -> 'a) ->
        set: ('a -> 'M -> 'M) ->
            FieldCodec<'M>

    /// A required `bool` field. Mirrors `boolAt`: only an explicit case-insensitive
    /// `true`/`false` is honoured, anything else reads as `fallback`. Always writes.
    val boolScalar: key: string -> fallback: bool -> get: ('M -> bool) -> set: (bool -> 'M -> 'M) -> FieldCodec<'M>

    /// A `string list` rendered as a YAML block sequence under `key:`. An empty
    /// list omits the line; absence reads as `[]`.
    val scalarBlock: key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>

    /// The YAML keys the field list owns, in declaration order. Used by the
    /// record-vs-codec coupling test (FS.GG.SDD#201 FR-007).
    val keys: fields: FieldCodec<'M> list -> string list

    /// Render a model to YAML by mapping every field's writer, in list order.
    /// Deterministic: output order is the field-list order; no clock/env/GUID.
    val render: fields: FieldCodec<'M> list -> model: 'M -> string

    /// Decode a model from YAML text by folding every field's reader over the
    /// parsed mapping, starting from `seed`. `Error` on an unparseable document,
    /// a non-mapping root, or a missing required field.
    val decode: fields: FieldCodec<'M> list -> seed: 'M -> text: string -> Result<'M, string>

    /// Decode a model by folding every field's reader over an already-parsed YAML
    /// mapping (e.g. one element of a block sequence), starting from `seed` — `decode`
    /// without the document-parse/root-mapping step, for nested records.
    val foldInto:
        fields: FieldCodec<'M> list ->
        seed: 'M ->
        mapping: YamlDotNet.RepresentationModel.YamlMappingNode ->
            Result<'M, string>
