namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System.Text.Json

/// Stable identity shared by specification models, extension nodes, and evidence obligations.
[<Struct>]
type SpecificationId = private SpecificationId of string

[<RequireQualifiedAccess>]
module SpecificationId =
    /// Create an uppercase ASCII identifier with at least five characters.
    val create: value: string -> Result<SpecificationId, string>

    /// Return the canonical text of an identifier.
    val value: id: SpecificationId -> string

/// One source location retained by migration and validation diagnostics.
type SourceLocation =
    { Line: int
      Column: int }

/// Authorship and authoritative-source provenance. Author/session/time are not semantic model bytes.
type SpecificationProvenance =
    { Agent: string
      Session: string
      SourcePath: string
      SourceRevision: string
      AuthoredAtUtc: string }

/// One semantic evidence obligation declared by a specification.
type EvidenceObligation =
    { Id: SpecificationId
      Kind: string
      Description: string }

/// One observed receipt offered to evidence validation.
type EvidenceReceipt =
    { ObligationId: SpecificationId
      Kind: string
      EvidenceRef: string }

/// Stable, path-addressed diagnostic suitable for machine and human projections.
type SpecificationDiagnostic =
    { Code: string
      Path: string
      Message: string
      Location: SourceLocation option }

/// Generic specification envelope. The consuming domain owns the concrete extension type.
type SpecificationModel<'extension> =
    { Identity: SpecificationId
      SchemaVersion: int
      Provenance: SpecificationProvenance
      Intent: string
      EvidenceObligations: EvidenceObligation list
      Extension: 'extension }

/// Explicit static contract for one concrete extension type; no boxing or reflection discovery is required.
type ExtensionContract<'extension> =
    { Kind: string
      SchemaVersion: int
      Validate: EvidenceObligation list -> 'extension -> SpecificationDiagnostic list
      EncodeCanonical: 'extension -> byte array
      WriteJson: Utf8JsonWriter -> 'extension -> unit
      DecodeJson: JsonElement -> Result<'extension, SpecificationDiagnostic list>
      ProjectMarkdown: 'extension -> string list }

/// Validated model plus its deterministic semantic representation.
type CompiledSpecification<'extension> =
    { Model: SpecificationModel<'extension>
      NormalizedBytes: byte array
      Fingerprint: string }

/// One stable semantic change between two specifications.
type SemanticChange =
    { Path: string
      Summary: string
      BeforeFingerprint: string
      AfterFingerprint: string }

/// Semantic comparison independent of author/session/time/intent noise.
type SemanticDiff =
    | Equivalent
    | Changed of SemanticChange list

/// Evidence validation retains all satisfied obligations and all detectable findings.
type EvidenceValidation =
    { Satisfied: SpecificationId list
      Diagnostics: SpecificationDiagnostic list }

/// A generated human/machine projection pair bound to one normalized source.
type SpecificationProjection =
    { Markdown: string
      Json: string
      SourceFingerprint: string
      GeneratedFingerprint: string }

/// Observation keeps absence and unreadability distinct at the pure validation boundary.
type ProjectionObservation =
    | Missing
    | Unreadable of detail: string
    | Content of text: string

/// Typed reasons retained when legacy authored content cannot migrate losslessly.
type MigrationReason =
    | UnresolvedReference
    | UnknownSemanticHeading
    | UnsupportedSchemaVersion
    | MalformedConstruct

/// One migration ambiguity or unsupported construct with its authored source location.
type MigrationFinding =
    { Code: string
      Reason: MigrationReason
      Message: string
      Location: SourceLocation }

/// Migration analysis never guesses and never writes.
type MigrationOutcome<'model> =
    | Migrated of 'model
    | Ambiguous of MigrationFinding list
    | Unsupported of MigrationFinding list

[<RequireQualifiedAccess>]
module SpecificationCompiler =
    /// Validate envelope and extension, returning every stable diagnostic in deterministic order.
    val validate:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        SpecificationDiagnostic list

    /// Return deterministic semantic bytes for a valid model.
    val normalize:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        Result<byte array, SpecificationDiagnostic list>

    /// Return lowercase SHA-256 over normalized semantic bytes.
    val fingerprint:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        Result<string, SpecificationDiagnostic list>

    /// Validate and compile one model.
    val compile:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        Result<CompiledSpecification<'extension>, SpecificationDiagnostic list>

    /// Compare two models by semantic components and stable fingerprints.
    val semanticDiff:
        contract: ExtensionContract<'extension> ->
        before: SpecificationModel<'extension> ->
        after: SpecificationModel<'extension> ->
        Result<SemanticDiff, SpecificationDiagnostic list>

[<RequireQualifiedAccess>]
module SpecificationCodec =
    /// Serialize schema-v1 deterministic JSON. Unknown envelope fields are never emitted.
    val serialize:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        Result<string, SpecificationDiagnostic list>

    /// Decode schema-v1 JSON through the concrete extension contract; unknown fields fail closed.
    val deserialize:
        contract: ExtensionContract<'extension> ->
        text: string ->
        Result<SpecificationModel<'extension>, SpecificationDiagnostic list>

[<RequireQualifiedAccess>]
module SpecificationProjection =
    /// Generate deterministic Markdown and JSON projections from one normalized model.
    val generate:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        Result<SpecificationProjection, SpecificationDiagnostic list>

    /// Validate Markdown observation, freshness, and generated-body integrity.
    val validateMarkdown:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        observation: ProjectionObservation ->
        SpecificationDiagnostic list

    /// Validate JSON observation, freshness, and generated-model integrity.
    val validateJson:
        contract: ExtensionContract<'extension> ->
        model: SpecificationModel<'extension> ->
        observation: ProjectionObservation ->
        SpecificationDiagnostic list

[<RequireQualifiedAccess>]
module SpecificationEvidence =
    /// Bind receipts to declared obligation ids and kinds without a Governance runtime.
    val validate:
        obligations: EvidenceObligation list ->
        receipts: EvidenceReceipt list ->
        EvidenceValidation
