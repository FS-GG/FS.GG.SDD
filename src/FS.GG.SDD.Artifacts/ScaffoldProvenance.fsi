namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.SchemaVersion

/// `.fsgg/scaffold-provenance.json` (schema v1): the byte-deterministic record of
/// who produced runtime files during `fsgg-sdd scaffold` and that their ongoing
/// ownership lies outside SDD. Authoritative for refresh exclusion (FR-006/FR-007).
module ScaffoldProvenance =
    type ScaffoldProducedPath =
        { Path: string
          Owner: ArtifactOwner }

    type ScaffoldProvenanceRecord =
        { SchemaVersion: int
          Generator: GeneratorVersion
          /// The provider-declared minimum coherent `fsgg-sdd` CLI version, recorded
          /// beside the producing CLI version (the `Generator`) for audit (feature 052,
          /// E1/FR-002). `None` when the provider declares none or declares a malformed
          /// minimum. Serialized `immediately after` `generator` as string-or-null;
          /// `tryParse` defaults absent/null to `None` (schema stays v1, additive).
          RequiredMinimumCliVersion: string option
          ProviderName: string
          ProviderContractVersion: string
          TemplateRef: string
          Outcome: string
          ProducedPaths: ScaffoldProducedPath list
          /// The effective `key → value` parameters forwarded to the provider —
          /// provider-declared `default`s overlaid by author `--param` overrides
          /// (author wins). Sorted ascending by key; `[]` when none. Records the
          /// chosen starter so a scaffolded product is auditable and reproducible
          /// (FR-003). Additive optional field; `tryParse` defaults it to `[]`
          /// for documents written before it (schema stays v1).
          EffectiveParameters: (string * string) list }

    /// The canonical project-relative path of the provenance artifact.
    val provenancePath: string

    /// Deterministic JSON (canonical key order, `producedPaths` sorted by path,
    /// no clock/absolute-path/ANSI) per the scaffold-provenance schema contract.
    val serialize: record: ScaffoldProvenanceRecord -> string

    /// Parse provenance JSON. Malformed or unsupported-schema content yields `None`
    /// (fail-safe: readers treat it as absent and surface the diagnostic).
    val tryParse: text: string -> ScaffoldProvenanceRecord option
