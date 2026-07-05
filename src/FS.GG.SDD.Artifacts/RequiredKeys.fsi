namespace FS.GG.SDD.Artifacts

/// Feature 081 (#140, FR-009): the single authoritative source of the field keys the
/// `fsgg-sdd` gates REQUIRE. The authored `fs-gg-sdd-*` skills and the authoring-contracts
/// §5 table are checked against these values (RequiredFieldContractTests), and the parsers'
/// own required-field enforcement is behaviourally confirmed against them — so a skill can
/// never omit a gate-required field, and a field an author reads about is always one the
/// gate actually enforces.
module RequiredKeys =

    /// The four fields an evidence declaration with `result: deferred` (`kind: deferral`)
    /// MUST carry, or the evidence gate blocks with `evidence.missingDeferralRationale`.
    val requiredDeferralKeys: string list

    /// The front-matter keys a stage's authored artifact MUST declare (absence blocks the
    /// gate). Defaulted keys that do not block are excluded. Stages whose artifacts carry
    /// no required front matter return the empty list.
    val requiredFrontMatterKeys: stage: Identifiers.LifecycleStage -> string list
