// CONTRACT SKETCH — Phase 1 design artifact, not compiled source.
// Implementation lives at src/FS.GG.Contracts/Provider.fsi (+ .fs).
// Extended provider descriptor (FR-006/007). Additive superset of SDD's current
// ProviderDescriptor: the five existing fields are preserved unchanged.

namespace Fsgg

module Provider =

    /// A declared build/test/run/verify command (Feature 035 H1 shape). Blank
    /// `Executable` is a MALFORMED declaration, distinct from absent/use-default.
    type DeclaredCommand =
        { Executable: string
          Arguments: string list }

    /// Preserved unchanged from SDD's current ProviderDescriptor.
    type ProviderParameterSpec =
        { Key: string
          Required: bool
          Default: string option }

    /// Extended template-provider descriptor. The first five fields are the exact
    /// current SDD record (additive guarantee, FR-006 Scenario 4); the rest are new.
    type ProviderDescriptor =
        { // --- preserved identity/parameters ---
          Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterSpec list
          // --- new optional declared commands (absent ⇒ platform default) ---
          Build: DeclaredCommand option
          Test: DeclaredCommand option
          Run: DeclaredCommand option
          Verify: DeclaredCommand option
          // --- new canonical product-name input parameter ---
          NameParameter: string }

    /// The default canonical name parameter when a provider declares none (FR-007).
    val defaultNameParameter: string            // = "name"

    /// Resolve the canonical name parameter, falling back to `defaultNameParameter`
    /// when the descriptor declares a blank/absent one (FR-007, Scenario 3).
    val resolveNameParameter: descriptor: ProviderDescriptor -> string

    /// True when a declared command is malformed (declared but blank executable),
    /// so consumers can surface it rather than silently default (spec Edge Case).
    val isMalformed: command: DeclaredCommand -> bool
