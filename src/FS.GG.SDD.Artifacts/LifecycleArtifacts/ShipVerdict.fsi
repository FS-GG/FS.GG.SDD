namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.SchemaVersion

/// Feature 092 / ADR-0026 — the compact, committed merge-boundary verdict.
///
/// `readiness/<id>/ship-verdict.json` is a pure projection of `readiness/<id>/ship.json`
/// that drops *inventory* and no *facts*: the `sources[]` digest list is replaced in place by
/// one aggregate `sourcesDigest`, which binds the verdict to the exact authored inputs. It is
/// the one lifecycle view whose value is commit-bound — regeneration reports today's
/// disposition, never the merge's — so it belongs to the *durable generated* class and is
/// committed, while `ship.json` stays regenerable and ignored.
///
/// Produced by `fsgg-sdd ship` and re-projected by `fsgg-sdd refresh` through one shared pure
/// function, which is what makes the two producers byte-identical by construction.
[<AutoOpen>]
module ShipVerdict =
    type ShipVerdict =
        {
            SchemaVersion: SchemaVersion
            ViewVersion: string
            WorkId: string
            Stage: string
            Status: string
            Generator: string
            SourcesDigest: SourceDigest
            VerificationReadinessStatus: string
            /// FS.GG.SDD#398 (FR-005). The attestation basis, in the ONE readiness artifact that is
            /// committed (`.gitignore` keeps `ship.json`/`verify.json` out of git and lets only this
            /// through). Without these, the record a reader finds in history is the bare string
            /// `shipReady` — and ADR-0026 made that record permanent.
            ///
            /// The projection "drops inventory and no facts". Counts are facts, and *"0 of 5 supported
            /// obligations were observed"* is the most consequential one the verdict has: it is the
            /// difference between "the paperwork is consistent" and "this works".
            EvidenceSupportedCount: int
            EvidenceSelfAttestedCount: int
            EvidenceObservedCount: int
            DispositionState: string
            DispositionBlockingFindingIds: string list
            Readiness: string
        }

    /// One aggregate SHA-256 over the canonical `sources[]` pre-image: the records sorted by
    /// path, rendered one per line as `<path>|<algorithm>:<value>` (a digest-less source
    /// contributes `<path>|`), joined with `\n` and no trailing newline.
    ///
    /// Binding each source's *path* to its digest — rather than hashing the digest values
    /// alone — is what lets a later reader prove the committed verdict corresponds to the
    /// committed sources. Exposed so tests can recompute it independently of the projection.
    val sourcesDigest: sources: AnalysisSourceRecord list -> SourceDigest

    /// Total, allocation-only projection. Every field is copied from the view except
    /// `SourcesDigest`, which summarizes the inventory it replaces.
    val fromShipView: view: ShipView -> ShipVerdict

    /// Canonical serialization: `ship.json`'s own top-level field order, with `sources`
    /// replaced in place by `sourcesDigest`. Byte-stable — no clock, no path, no ANSI.
    /// Exactly 23 lines when `DispositionBlockingFindingIds` is empty, plus one line per id.
    /// (20 before FS.GG.SDD#398 added the three `verificationReadiness` attestation counts.)
    val toJson: verdict: ShipVerdict -> string
