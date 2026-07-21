namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.SchemaVersion

/// `.fsgg/scaffold-provenance.json` (schema v1): the byte-deterministic record of
/// who produced runtime files during `fsgg-sdd scaffold` and that their ongoing
/// ownership lies outside SDD. Authoritative for refresh exclusion (FR-006/FR-007).
module ScaffoldProvenance =
    type ScaffoldProducedPath =
        {
            Path: string
            Owner: ArtifactOwner
            /// Additive (contract 1.1.0, ADR-0014): the per-path content digest.
            /// `None` ⇒ no digest recorded (a 1.0.0 document, or a path not yet
            /// hashed). Serialized only when `Some`; parse defaults absent to `None`.
            Sha256: string option
        }

    type ScaffoldProvenanceRecord =
        {
            SchemaVersion: int
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
            /// 056: the `.claude`/`.codex` mirror copies of the provider's produced
            /// `.agents/skills/*` skills that SDD fanned out (owner `Mirrored`). The
            /// provider's canonical `.agents` skill stays in `ProducedPaths`
            /// (`GeneratedProduct`); no seeded `fs-gg-sdd-*` path appears here. Additive
            /// optional field, sorted ascending by path, serialized immediately after
            /// `producedPaths`; `tryParse` defaults absent/null to `[]` (schema stays v1).
            MirroredPaths: ScaffoldProducedPath list
            /// The files SDD itself wrote during a post-instantiation step (owner `Sdd`) —
            /// currently `.config/dotnet-tools.json`, the CLI pin (FS.GG.SDD#315). Kept out of
            /// `ProducedPaths`, which the app-only invariant defines as **exactly** the
            /// provider's tree (specs/031 P1/P3): an SDD-written file there would break both
            /// precision and skeleton-disjointness. Recording it here classifies it as
            /// SDD-owned rather than externally owned. Sorted ascending by path, serialized
            /// immediately after `mirroredPaths`; `tryParse` defaults absent/null to `[]`
            /// (schema stays v1, additive). Empty when the step was skipped or preserved.
            SddOwnedPaths: ScaffoldProducedPath list
            /// 108 / ADR-0054: the `.github`-authored **driver** skills (e.g. `workRoadmap`)
            /// materialized from the pinned `FS.GG.Drivers` package into the product's skill
            /// roots (owner `Driver`), each with its content `sha256`. Externally owned like
            /// `MirroredPaths` — `refresh` excludes them (never regenerated). Sorted ascending
            /// by path, serialized immediately after `sddOwnedPaths`; `tryParse` defaults
            /// absent/null to `[]` (schema stays v1, additive). Empty when none materialized.
            DriverPaths: ScaffoldProducedPath list
            /// ADR-0063 / FS.GG.SDD#623: the owner-authored **product** skills (e.g.
            /// `fs-gg-playtest`, `mirrored: false`) materialized from the pinned
            /// the owner-skills package into the product's skill roots (owner `GameSkill`),
            /// each with its content `sha256`. Externally owned like `DriverPaths` — `refresh`
            /// excludes them (never regenerated). Sorted ascending by path, serialized immediately
            /// after `driverPaths`; `tryParse` defaults absent/null to `[]` (schema stays v1,
            /// additive). Empty when none materialized.
            GameSkillPaths: ScaffoldProducedPath list
            /// The effective `key → value` parameters forwarded to the provider —
            /// provider-declared `default`s overlaid by author `--param` overrides
            /// (author wins). Sorted ascending by key; `[]` when none. Records the
            /// chosen starter so a scaffolded product is auditable and reproducible
            /// (FR-003). Additive optional field; `tryParse` defaults it to `[]`
            /// for documents written before it (schema stays v1).
            EffectiveParameters: (string * string) list
        }

    /// The canonical project-relative path of the provenance artifact.
    val provenancePath: string

    /// The `outcome` marker for a provider-less **dev-repo** provenance document written
    /// by `fsgg-sdd init` (feature 085): no provider/template pin, the seeded SDD skeleton
    /// as `producedPaths` (owner `Sdd`). Distinguishes a dev-repo from a scaffolded product
    /// without a schema bump — the record stays schema v1, so existing readers parse it and
    /// `doctor`/`upgrade` engage instead of the "no provenance — nothing to reconcile" hole.
    val devRepoOutcome: string

    /// True when the record is a dev-repo document (`Outcome = devRepoOutcome`): produced by
    /// `init`, not `scaffold`, so it carries no provider/template pin (empty provider fields).
    val isDevRepo: record: ScaffoldProvenanceRecord -> bool

    /// Build the canonical dev-repo provenance record: empty provider/contract/template, no
    /// required minimum, `devRepoOutcome`, the given `producedPaths` (the seeded skeleton,
    /// owner `Sdd`), and empty mirrored/sdd-owned/effective sets. Deterministic — no
    /// clock/randomness, so `init` stays byte-identical for a given CLI version (FR-007).
    val devRepoRecord:
        generator: GeneratorVersion -> producedPaths: ScaffoldProducedPath list -> ScaffoldProvenanceRecord

    /// Deterministic JSON (canonical key order, `producedPaths` sorted by path,
    /// no clock/absolute-path/ANSI) per the scaffold-provenance schema contract.
    val serialize: record: ScaffoldProvenanceRecord -> string

    /// Parse provenance JSON. Malformed or unsupported-schema content yields `None`
    /// (fail-safe: readers treat it as absent and surface the diagnostic).
    val tryParse: text: string -> ScaffoldProvenanceRecord option
