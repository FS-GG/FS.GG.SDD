# Single Quint-backed workspace lifecycle

This literate document is the model authority for the accepted workspace and its revision-bound
proposals. Freeform prose, structured SDD, and direct Quint are authoring depths over this same
state; filing and classification never mutate accepted authority. The generated
`workspace-lifecycle.qnt` file is a projection, not another source to edit.

```quint workspace-lifecycle.qnt +=
module WorkspaceLifecycle {
  type ProposalPhase = NoProposal | Filed | Classified | Accepted

  pure val authoringDepths = Set("Freeform", "StructuredSdd", "DirectQuint")
  pure val workspaceModuleKinds = Set(
    "ProductSpecification",
    "Decision",
    "WorkChange",
    "RepositoryProfile",
    "CiObligation",
    "ExternalContract",
    "EvidenceRequirement"
  )
  pure val proposalDispositions = Set(
    "CoherentDelta",
    "NoSemanticChange",
    "AcceptedOpaque",
    "Ambiguous",
    "Contradictory",
    "Stale"
  )
  pure val eligibleDispositions = Set("CoherentDelta", "NoSemanticChange", "AcceptedOpaque")

  var acceptedRevision: int
  var acceptedFingerprint: int
  var acceptedCoherent: bool
  var proposalPhase: ProposalPhase
  var proposalBaseRevision: int
  var proposalDisposition: str
  var authorityAtFiling: int
  var lastAction: str

  action init = all {
    acceptedRevision' = 0,
    acceptedFingerprint' = 0,
    acceptedCoherent' = true,
    proposalPhase' = NoProposal,
    proposalBaseRevision' = -1,
    proposalDisposition' = "",
    authorityAtFiling' = -1,
    lastAction' = "Init",
  }

  action fileIssue = all {
    proposalPhase == NoProposal,
    acceptedRevision' = acceptedRevision,
    acceptedFingerprint' = acceptedFingerprint,
    acceptedCoherent' = acceptedCoherent,
    proposalPhase' = Filed,
    proposalBaseRevision' = acceptedRevision,
    proposalDisposition' = "",
    authorityAtFiling' = acceptedFingerprint,
    lastAction' = "FileIssue",
  }

  action classifyAs(disposition: str): bool = all {
    proposalPhase == Filed,
    proposalDispositions.contains(disposition),
    acceptedRevision' = acceptedRevision,
    acceptedFingerprint' = acceptedFingerprint,
    acceptedCoherent' = acceptedCoherent,
    proposalPhase' = Classified,
    proposalBaseRevision' = proposalBaseRevision,
    proposalDisposition' = disposition,
    authorityAtFiling' = authorityAtFiling,
    lastAction' = "Classify",
  }

  action accept = all {
    proposalPhase == Classified,
    eligibleDispositions.contains(proposalDisposition),
    proposalBaseRevision == acceptedRevision,
    acceptedRevision' = acceptedRevision + 1,
    acceptedFingerprint' = acceptedFingerprint + 1,
    acceptedCoherent' = true,
    proposalPhase' = Accepted,
    proposalBaseRevision' = proposalBaseRevision,
    proposalDisposition' = proposalDisposition,
    authorityAtFiling' = authorityAtFiling,
    lastAction' = "Accept",
  }

  action advanceAcceptedBase = all {
    proposalPhase == Filed,
    acceptedRevision' = acceptedRevision + 1,
    acceptedFingerprint' = acceptedFingerprint + 1,
    acceptedCoherent' = true,
    proposalPhase' = proposalPhase,
    proposalBaseRevision' = proposalBaseRevision,
    proposalDisposition' = proposalDisposition,
    authorityAtFiling' = authorityAtFiling,
    lastAction' = "AdvanceAcceptedBase",
  }

  val filingIsImmutable =
    (lastAction == "FileIssue") implies acceptedFingerprint == authorityAtFiling

  val acceptedAuthorityIsCoherent = acceptedCoherent
  val acceptedRevisionIsMonotonic = acceptedRevision >= 0
  val acceptedFingerprintTracksRevision = acceptedFingerprint == acceptedRevision
  val acceptanceUsesExactBase =
    (proposalPhase == Accepted) implies acceptedRevision == proposalBaseRevision + 1
  val staleProposalCannotAccept =
    (proposalBaseRevision != acceptedRevision and proposalPhase == Classified)
      implies lastAction != "Accept"

  action step = any {
    fileIssue,
    classifyAs("CoherentDelta"),
    classifyAs("NoSemanticChange"),
    classifyAs("AcceptedOpaque"),
    classifyAs("Ambiguous"),
    classifyAs("Contradictory"),
    classifyAs("Stale"),
    accept,
    advanceAcceptedBase,
  }
}

module WorkspaceLifecycleTests {
  import WorkspaceLifecycle.*

  run filingDoesNotMutateAcceptedAuthority =
    init
      .then(fileIssue)
      .expect(and {
        proposalPhase == Filed,
        filingIsImmutable,
        acceptedRevision == 0,
        acceptedFingerprint == 0,
      })

  run coherentDeltaCanReachAcceptance =
    init
      .then(fileIssue)
      .then(classifyAs("CoherentDelta"))
      .then(accept)
      .expect(and {
        proposalPhase == Accepted,
        acceptedRevision == 1,
        acceptedAuthorityIsCoherent,
        acceptanceUsesExactBase,
        acceptedFingerprintTracksRevision,
      })

  run noSemanticChangeCanReachAcceptance =
    init
      .then(fileIssue)
      .then(classifyAs("NoSemanticChange"))
      .then(accept)
      .expect(acceptanceUsesExactBase)

  run acceptedOpaqueCanReachAcceptance =
    init
      .then(fileIssue)
      .then(classifyAs("AcceptedOpaque"))
      .then(accept)
      .expect(acceptanceUsesExactBase)

  run ambiguousProposalRemainsNonAccepting =
    init
      .then(fileIssue)
      .then(classifyAs("Ambiguous"))
      .expect(and {
        proposalPhase == Classified,
        not(eligibleDispositions.contains(proposalDisposition)),
        acceptedRevision == 0,
      })

  run contradictoryProposalRemainsNonAccepting =
    init
      .then(fileIssue)
      .then(classifyAs("Contradictory"))
      .expect(not(eligibleDispositions.contains(proposalDisposition)))

  run staleProposalRemainsNonAccepting =
    init
      .then(fileIssue)
      .then(advanceAcceptedBase)
      .then(classifyAs("Stale"))
      .expect(and {
        proposalBaseRevision != acceptedRevision,
        not(eligibleDispositions.contains(proposalDisposition)),
        acceptedAuthorityIsCoherent,
        acceptedRevisionIsMonotonic,
      })
}
```

The intentionally separate `advanceAcceptedBase` action makes staleness reachable without allowing
proposal work to mutate the accepted model. Acceptance is enabled only for the three maintainer-
approved accepting dispositions at the exact recorded base.
