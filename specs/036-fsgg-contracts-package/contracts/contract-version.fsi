// CONTRACT SKETCH — Phase 1 design artifact, not compiled source.
// Implementation lives at src/FS.GG.Contracts/ContractVersion.fsi (+ .fs).
// Self-describing package contract version (FR-012): a consumer can detect which
// contract surface it compiled against without reflecting over NuGet metadata.

namespace Fsgg

module ContractVersion =

    /// The published package contract SemVer.
    val value: string // = "1.0.0"

    val major: int // = 1
    val minor: int // = 0
    val patch: int // = 0
