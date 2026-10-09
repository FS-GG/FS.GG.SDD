namespace FS.GG.SDD.Cli

/// Explicit catalog inspection and selected scaffold invocation; legacy routes remain separate.
module Catalog =
    /// Parsed caller selections only; parsing performs no reads, probes or native preparation.
    type ScaffoldOptions =
        {
            CatalogPath: string
            CatalogDigest: string
            Provider: string
            TargetRoot: string
            TemplateArchive: string
            TemplateDigest: string
            PolicyPath: string
            PolicyDigest: string
            Platform: string
            TransportExecutable: string
            PreflightSeconds: int
            ScaffoldSeconds: int
            Overrides: (string * string) list
            DryRun: bool
        }

    /// Closed argv parsing preserves ordinal parameter values, including empty strings.
    val parseScaffoldOptions: args: string list -> Result<ScaffoldOptions, Fsgg.ProviderCatalog.Diagnostic list>
    /// Runs the selected production edge once, retaining its original owner after unresolved cleanup.
    /// Reporting does not promise bounded process exit; passive waiting grants no cleanup authority.
    val scaffold: args: string list -> int
    val run: args: string list -> int
    val unavailable: args: string list -> int
