namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.SchemaVersion

/// SDD-owned release contract (feature 018). A pure projection over the package
/// version identity, the compatibility matrix, and a catalog describing every
/// public generated view and `--json` command-output report. It adds no lifecycle
/// stage and changes no authored-source schema (FR-013); it documents and locks
/// the contracts that already exist. The serialized form is the authoritative
/// machine artifact `docs/release/release-readiness.json`; the Markdown docs are
/// projections of it (FR-005). It is entirely SDD-owned: no Governance gate,
/// route, profile, freshness, or publish/provenance data (FR-014) — Governance
/// compatibility appears only as an optional declared `contractVersion` range.
module ReleaseContract =
    /// Release line maturity, derived from the SemVer major (FR-003).
    type ReleaseChannel =
        | PreRelease
        | StableRelease

    /// Classification of a public-contract change, driving the SemVer bump rule
    /// (FR-001).
    type ChangeClass =
        | Breaking
        | Additive
        | Clarifying

    /// Per-contract / per-field stability classification (FR-004).
    type StabilityClass =
        | Stable
        | AdditiveOptional
        | Experimental

    /// Serialization format of a catalogued contract: a JSON machine contract or a
    /// Markdown projection (Constitution II authoring surface).
    type ContractFormat =
        | Json
        | Markdown

    /// What kind of public output a catalog entry documents, plus its format.
    type ContractKind =
        | GeneratedViewContract of GeneratedViewKind * ContractFormat
        | CommandOutputContract

    /// Whether an inventory item is a JSON field or a Markdown document section.
    type InventoryKind =
        | JsonField
        | MarkdownSection

    /// One documented field (JSON) or section (Markdown) with its own stability.
    type InventoryItem =
        { Name: string
          Kind: InventoryKind
          Stability: StabilityClass }

    /// The single declared semantic version shared by every `FS.GG.SDD.*` package
    /// and the `fsgg-sdd` CLI (FR-003).
    type PackageVersionIdentity =
        { Version: string
          Channel: ReleaseChannel
          PackageIds: string list
          CliCommandName: string }

    /// A per-release-line compatibility record. The Governance range is an optional
    /// integration fact that MUST NOT block readiness (FR-002).
    type CompatibilityMatrixEntry =
        { SddVersionLine: string
          SpecKitRange: string
          GovernanceContractVersionRange: string option }

    /// The documented shape of one public generated artifact (or sub-file) or
    /// `--json` report.
    type SchemaReferenceEntry =
        {
            Contract: string
            Kind: ContractKind
            SchemaVersion: int
            ContractVersion: string option
            Stability: StabilityClass
            Determinism: string
            Inventory: InventoryItem list
            SourceArtifact: ArtifactRef
            BaselinePresent: bool
            /// Feature 092 / ADR-0026. `true` for a *durable generated* artifact: machine-emitted,
            /// byte-stable, drift-guarded, and **committed**. The taxonomy doc partitions the
            /// `generatedView` catalog on this flag — regenerable table when `false`, durable table
            /// when `true` — so the doc stays catalog-derived rather than hand-maintained.
            DurableGenerated: bool
        }

    /// A per-release record pointer for breaking changes (FR-009/FR-010).
    type MigrationNoteRef =
        { Version: string
          Path: string
          BreakingChanges: string list }

    /// The authoritative machine contract serialized to
    /// `docs/release/release-readiness.json`.
    type ReleaseReadiness =
        { SchemaVersion: int
          GeneratorVersion: GeneratorVersion
          Identity: PackageVersionIdentity
          Compatibility: CompatibilityMatrixEntry list
          Catalog: SchemaReferenceEntry list
          Migrations: MigrationNoteRef list }

    /// A caller-supplied snapshot of what a real lifecycle run produced, fed to the
    /// pure `evaluate` check (the caller does the file I/O — Constitution V).
    type ProducedArtifact =
        { Contract: string
          Source: ArtifactRef
          Inventory: string list }

    val releaseChannelValue: channel: ReleaseChannel -> string
    val changeClassValue: changeClass: ChangeClass -> string

    /// Every distinct JSON field path to full depth, as dotted paths (`parent.child`, arrays as
    /// `parent[].child`, elements deduplicated); `[]` for a non-object or unparseable root. The
    /// observed key set the release drift check walks so nested drift is visible to `evaluate`
    /// (ADR-0002 Gap B finding 6 / #261).
    val fullDepthKeys: text: string -> string list
    val stabilityClassValue: stability: StabilityClass -> string
    val contractFormatValue: format: ContractFormat -> string
    val inventoryKindValue: kind: InventoryKind -> string

    /// Derive the release channel from a SemVer string: major `0` ⇒ `PreRelease`.
    val channelOfVersion: version: string -> ReleaseChannel

    /// The SemVer bump implied by a change class: `Breaking` ⇒ `major`,
    /// `Additive` ⇒ `minor`, `Clarifying` ⇒ `patch` (FR-001).
    val bumpRule: changeClass: ChangeClass -> string

    /// Whether a change class obliges a migration note: `Breaking` only
    /// (FR-009/FR-010).
    val migrationNoteRequired: changeClass: ChangeClass -> bool

    /// The current release contract — the single authored source serialized to
    /// `docs/release/release-readiness.json` (FR-002/FR-003/FR-004).
    val currentRelease: unit -> ReleaseReadiness

    /// Canonical, deterministic JSON serialization through stable key/element
    /// ordering with no clock, host path, or ANSI styling (FR-008).
    val serialize: release: ReleaseReadiness -> string

    /// Parse the canonical JSON back into a `ReleaseReadiness` (round-trip).
    val parse: json: string -> Result<ReleaseReadiness, string>

    /// Pure readiness check: given the catalog and a snapshot of produced
    /// artifacts, return one diagnostic per gap (output with no entry, entry with
    /// no baseline or empty source, or field-level drift). An empty list ⇒
    /// release-ready (FR-012/FR-015). No file I/O (Constitution V).
    val evaluate: release: ReleaseReadiness -> produced: ProducedArtifact list -> Diagnostic list
