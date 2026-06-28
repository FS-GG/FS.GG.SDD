namespace Fsgg

/// Extended template-provider descriptor (FR-006/007). Additive superset of SDD's
/// current `ProviderDescriptor`: the five existing fields are preserved unchanged;
/// the four declared commands and the canonical name parameter are new.
module Provider =

    /// A declared build/test/run/verify command (Feature 035 H1 shape). Blank
    /// `Executable` is a MALFORMED declaration, distinct from absent/use-default.
    type DeclaredCommand =
        { Executable: string
          Arguments: string list }

    /// Preserved unchanged from SDD's current `ProviderDescriptor`.
    type ProviderParameterSpec =
        { Key: string
          Required: bool
          Default: string option }

    /// Extended template-provider descriptor. The first five fields are the exact
    /// current SDD record (additive guarantee, FR-006 Scenario 4); the rest are new.
    type ProviderDescriptor =
        { Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterSpec list
          Build: DeclaredCommand option
          Test: DeclaredCommand option
          Run: DeclaredCommand option
          Verify: DeclaredCommand option
          NameParameter: string }

    /// The default canonical name parameter when a provider declares none (FR-007).
    val defaultNameParameter: string

    /// Resolve the canonical name parameter, falling back to `defaultNameParameter`
    /// when the descriptor declares a blank/whitespace one (FR-007, Scenario 3).
    val resolveNameParameter: descriptor: ProviderDescriptor -> string

    /// True when a declared command is malformed (declared but blank/whitespace
    /// executable), so consumers can surface it rather than silently default
    /// (spec Edge Case, Principle VIII).
    val isMalformed: command: DeclaredCommand -> bool
