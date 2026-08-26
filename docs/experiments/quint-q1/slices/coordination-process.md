# Concurrent coordination process slice

This plain-Quint model isolates the authority hazards relevant to GitHub Substrate v2 without introducing
a network-message framework. A candidate begins at revision 1. A worker observes that revision, prepares
one mutation, and may apply it exactly once only while the live revision still matches. Lost responses
may cause retries, but the durable receipt makes retry idempotent. A stale observer is refused and must
refresh. Completion requires the receipt.

The stable actions are `Prepare`, `Interfere`, `Apply`, `LoseResponse`, `Retry`, `Refresh`, and `Complete`.
Their reads/writes are explicit in `actionCatalogue`, so no prose-only transition exists.

```quint coordination.qnt +=
module CoordinationSlice {
  type Phase = Idle | Prepared | Applied | Complete
  type ActionEntry = { id: str, reads: Set[str], writes: Set[str] }

  pure val actionCatalogue = Set(
    { id: "Prepare", reads: Set("revision"), writes: Set("phase", "observedRevision") },
    { id: "Interfere", reads: Set("revision"), writes: Set("revision") },
    { id: "Apply", reads: Set("revision", "observedRevision", "receipt"), writes: Set("revision", "receipt", "applyCount") },
    { id: "RefuseStale", reads: Set("revision", "observedRevision"), writes: Set("staleRefused") },
    { id: "LoseResponse", reads: Set("receipt", "lossCount"), writes: Set("responseLost", "lossCount") },
    { id: "Retry", reads: Set("receipt", "responseLost"), writes: Set("retryCount", "responseLost") },
    { id: "Refresh", reads: Set("revision"), writes: Set("observedRevision", "phase") },
    { id: "Complete", reads: Set("receipt"), writes: Set("phase") }
  )

  // Verification bounds are not production-domain constants. They close the Q1 state space while
  // leaving enough revisions for two stale-observation cycles followed by one successful apply.
  pure val MAX_REVISION = 4

  var phase: Phase
  var revision: int
  var observedRevision: int
  var receipt: bool
  var responseLost: bool
  var applyCount: int
  var retryCount: int
  var staleRefused: bool
  var lossCount: int

  action init = all {
    phase' = Idle,
    revision' = 1,
    observedRevision' = 0,
    receipt' = false,
    responseLost' = false,
    applyCount' = 0,
    retryCount' = 0,
    staleRefused' = false,
    lossCount' = 0,
  }

  action prepare = all {
    phase == Idle,
    phase' = Prepared,
    observedRevision' = revision,
    revision' = revision,
    receipt' = receipt,
    responseLost' = responseLost,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action interfere = all {
    phase == Prepared,
    revision < MAX_REVISION - 1,
    phase' = phase,
    revision' = revision + 1,
    observedRevision' = observedRevision,
    receipt' = receipt,
    responseLost' = responseLost,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action apply = all {
    phase == Prepared,
    observedRevision == revision,
    revision < MAX_REVISION,
    not(receipt),
    phase' = Applied,
    revision' = revision + 1,
    observedRevision' = observedRevision,
    receipt' = true,
    responseLost' = responseLost,
    applyCount' = applyCount + 1,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action refuseStale = all {
    phase == Prepared,
    observedRevision != revision,
    phase' = phase,
    revision' = revision,
    observedRevision' = observedRevision,
    receipt' = receipt,
    responseLost' = responseLost,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = true,
    lossCount' = lossCount,
  }

  action loseResponse = all {
    phase == Applied,
    receipt,
    lossCount == 0,
    phase' = phase,
    revision' = revision,
    observedRevision' = observedRevision,
    receipt' = receipt,
    responseLost' = true,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount + 1,
  }

  action retry = all {
    receipt,
    responseLost,
    phase' = Applied,
    revision' = revision,
    observedRevision' = observedRevision,
    receipt' = receipt,
    responseLost' = false,
    applyCount' = applyCount,
    retryCount' = retryCount + 1,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action refresh = all {
    phase == Prepared,
    observedRevision != revision,
    phase' = Idle,
    revision' = revision,
    observedRevision' = revision,
    receipt' = receipt,
    responseLost' = responseLost,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action complete = all {
    phase == Applied,
    receipt,
    not(responseLost),
    phase' = Complete,
    revision' = revision,
    observedRevision' = observedRevision,
    receipt' = receipt,
    responseLost' = responseLost,
    applyCount' = applyCount,
    retryCount' = retryCount,
    staleRefused' = staleRefused,
    lossCount' = lossCount,
  }

  action progress = any { prepare, apply, retry, refresh, complete }
  action step = any { progress, interfere, refuseStale, loseResponse }

  val atMostOneApply = applyCount <= 1
  val receiptMatchesApply = receipt == (applyCount == 1)
  val completeHasReceipt = phase == Complete implies receipt
  val staleNeverApplies =
    (phase == Prepared and observedRevision != revision) implies
      (applyCount == 0 and not(receipt))
  val knownPhase = Set(Idle, Prepared, Applied, Complete).contains(phase)
  temporal eventualCompletion = progress.weakFair(
    Set((phase, revision, observedRevision, receipt, responseLost, applyCount, retryCount, staleRefused, lossCount))
  ) implies eventually(phase == Complete)
}
```

The two examples separate stale-observation recovery from lost-response retry. The second proves that
retry changes only retry/response state: it cannot increment `applyCount` or revision a second time.

```quint coordination.qnt +=
module CoordinationSliceTests {
  import CoordinationSlice.*

  run staleObservationIsRefused =
    init
      .then(prepare)
      .then(interfere)
      .then(refuseStale)
      .expect(and { staleRefused, applyCount == 0, not(receipt) })
      .then(refresh)
      .then(prepare)
      .then(apply)
      .then(complete)
      .expect(and { phase == Complete, atMostOneApply, receiptMatchesApply, completeHasReceipt })

  run lostResponseRetryIsIdempotent =
    init
      .then(prepare)
      .then(apply)
      .then(loseResponse)
      .then(retry)
      .then(complete)
      .expect(and {
        phase == Complete,
        applyCount == 1,
        retryCount == 1,
        revision == 2,
        atMostOneApply,
        receiptMatchesApply,
      })
}
```

The required mutations remove the revision equality, increment `applyCount` on retry, permit completion
without a receipt, or make stale refusal apply anyway. Each must fail a named invariant or example.
