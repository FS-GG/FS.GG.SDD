namespace FS.GG.SDD.Artifacts

/// Feature 081 (#140, FR-009): the single authoritative source of the field keys the
/// `fsgg-sdd` gates REQUIRE. The authored `fs-gg-sdd-*` skills and the authoring-contracts
/// §5 "Gating fields" column are checked against these values (RequiredFieldContractTests),
/// so a skill or the doc can never omit a gate-required field. The deferral keys are
/// additionally confirmed BEHAVIOURALLY against the evidence gate (omit any one → block);
/// the front-matter keys mirror each parser's required-field tuple (verified against the
/// `Some … , [] ->` matches and the live charter/evidence gates).
module RequiredKeys =

    /// The four fields an evidence declaration with `result: deferred` (`kind: deferral`)
    /// MUST carry, or the evidence gate blocks with `evidence.missingDeferralRationale`.
    val requiredDeferralKeys: string list

    /// The front-matter keys a stage's authored artifact MUST declare (absence blocks the
    /// gate). Defaulted keys that do not block are excluded. Stages whose artifacts carry
    /// no required front matter return the empty list.
    val requiredFrontMatterKeys: stage: Identifiers.LifecycleStage -> string list
