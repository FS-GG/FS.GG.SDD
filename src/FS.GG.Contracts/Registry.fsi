namespace Fsgg

/// Typed cross-repo dependency-registry model + pure validator (FR-008/009).
/// BCL-only: SemVer parse/compare/range is an internal helper, no third-party package.
module Registry =

    /// A cross-repo component (repo/package) and its declared version.
    type RegistryComponent =
        { Id: string
          Version: string }

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
    type RegistryRule =
        | MissingField of fieldName: string
        | UnknownComponent
        | IncompatibleVersion
        | MalformedVersion

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
