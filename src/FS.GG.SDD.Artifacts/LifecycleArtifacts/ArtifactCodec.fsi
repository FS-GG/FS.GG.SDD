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

    /// A `string list` rendered as a YAML flow sequence `key: [a, b, c]`. An
    /// empty list omits the line; absence reads as `[]`.
    val inlineList: key: string -> get: ('M -> string list) -> set: (string list -> 'M -> 'M) -> FieldCodec<'M>

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
