namespace FS.GG.SDD.Artifacts

open FS.GG.SDD.Artifacts.Identifiers

module RequiredKeys =

    // The keys below mirror each parser's required-field tuple (the `Some … , [] ->` match
    // that admits the artifact). They are behaviourally confirmed against the parsers by
    // RequiredFieldContractTests, so this list cannot silently drift from what the gate enforces.
    let requiredDeferralKeys =
        [ "rationale"; "owner"; "scope"; "laterLifecycleVisibility" ]

    let requiredFrontMatterKeys (stage: LifecycleStage) : string list =
        // The universal identity keys every front-matter artifact must carry.
        let identity = [ "schemaVersion"; "workId"; "stage" ]

        match stage with
        // charter front matter is strict — it gates on every identity field, no defaulting.
        | Charter -> identity @ [ "title"; "changeTier"; "status" ]
        | Specify -> identity
        | Clarify -> identity @ [ "sourceSpec" ]
        | Checklist -> identity @ [ "sourceSpec"; "sourceClarifications" ]
        | Plan -> identity @ [ "sourceSpec"; "sourceClarifications"; "sourceChecklist" ]
        // tasks.yml gates only on the schema version (workId is derivable from the path);
        // evidence.yml additionally gates on a valid workId. Body obligations aren't front matter.
        | Tasks -> [ "schemaVersion" ]
        | Evidence -> [ "schemaVersion"; "workId" ]
        // analyze/verify/ship/implement do not author a front-matter artifact (generated
        // readiness views or a non-artifact stage), so they require no authored keys.
        | Analyze
        | Implement
        | Verify
        | Ship -> []
