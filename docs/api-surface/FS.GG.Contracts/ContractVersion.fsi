namespace Fsgg

/// Self-describing package contract version (FR-012): a consumer can detect which
/// contract surface it compiled against without reflecting over NuGet metadata.
/// Single authoritative value — no second place can disagree.
module ContractVersion =

    /// The published package contract SemVer.
    val value: string

    val major: int
    val minor: int
    val patch: int
