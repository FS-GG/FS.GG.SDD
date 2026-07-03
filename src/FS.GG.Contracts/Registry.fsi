namespace Fsgg

/// Typed cross-repo dependency-registry model + pure validator (FR-008/009).
/// BCL-only: SemVer parse/compare/range is an internal helper, no third-party package.
module Registry =

    /// A cross-repo component (repo/package) and its declared version.
    type RegistryComponent = { Id: string; Version: string }

    /// A dependency edge: `Consumer` depends on `Provider`, declaring the range of
    /// provider versions it is compatible with.
    type DependencyEdge =
        { Consumer: string
          Provider: string
          CompatibleRange: string }

    /// The typed model of `registry/dependencies.yml`.
    type RegistryModel =
        { Components: RegistryComponent list
          Edges: DependencyEdge list }

    /// The coherence/completeness rule a diagnostic reports as violated (FR-009).
    /// `DuplicateComponent` and `MalformedDocument` were added (additively, feature
    /// 042) for the real-schema `validateDocument`; the legacy `validate` emits only
    /// the original four.
    type RegistryRule =
        | MissingField of fieldName: string
        | UnknownComponent
        | IncompatibleVersion
        | MalformedVersion
        | DuplicateComponent
        | MalformedDocument

    /// A single actionable diagnostic naming the offending entry and the rule.
    type RegistryDiagnostic =
        { Entry: string
          Rule: RegistryRule
          Message: string }

    /// Validation outcome: success has no diagnostics (SC-007).
    type ValidationResult =
        | Valid
        | Invalid of RegistryDiagnostic list

    /// Pure validator over the typed model. Reports missing required fields,
    /// edges referencing unknown components, version ranges that exclude the
    /// referenced version, and malformed version strings (FR-008/009).
    val validate: model: RegistryModel -> ValidationResult

    // --- Real-schema document model + pure validator (feature 042, additive). ---
    // Models the actual `registry/dependencies.yml` shape (schemaVersion / repos /
    // contracts[] / dependencies[] / coherence[]). The legacy RegistryModel/validate
    // above are retained unchanged. The YAML `load` edge lives in FS.GG.SDD.Artifacts
    // (Constitution V — I/O at the edge, not in this BCL-only leaf).

    /// A repo participating in the registry (the `repos:` map). `Id` is the map key.
    type RegistryRepo =
        { Id: string
          Name: string
          Role: string }

    /// A versioned cross-repo contract (`contracts[]`). `PackageVersion`/`Range` are
    /// present only on some entries.
    type ContractEntry =
        { Id: string
          Version: string
          Owner: string
          Surface: string
          Consumers: string list
          PackageVersion: string option
          Range: string option }

    /// A hard dependency edge over repos (`dependencies[]`). `From`/`To` are repo
    /// ids; `Via` is free-text and is NOT contract-checked (parity with the Python
    /// authority — research R4).
    type DependencyEdge2 =
        { From: string
          To: string
          Via: string }

    /// A coherence state entry (`coherence[]`).
    type CoherenceEntry = { Id: string; Coherent: bool }

    /// The typed model of the real `registry/dependencies.yml`.
    type RegistryDocument =
        { SchemaVersion: int
          Repos: RegistryRepo list
          Contracts: ContractEntry list
          Dependencies: DependencyEdge2 list
          Coherence: CoherenceEntry list }

    /// Pure validator over the real-schema document. Mirrors the rule *kinds* of
    /// scripts/validate-registry.py so the two cannot disagree on the canonical file
    /// (SC-005). Deterministic: diagnostics in document order
    /// (root → repos → contracts → dependencies → coherence). No I/O.
    val validateDocument: document: RegistryDocument -> ValidationResult
