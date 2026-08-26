# S.I.R. damage-rule correspondence slice

This is a producer-owned reference model, not S.I.R. production authority. The stable replay vocabulary
is `Initialize`, `ApplyDamage(amount)`, and the observation `{ hitPoints, lastAction, lastAmount }`.
`EHotwagner/S.I.R.#353` owns mapping those identities to the real interpreter. This document deliberately
contains no F# call, consumer source reference, or copied interpreter implementation.

The safety rule is executable: damage clamps hit points at zero and records the exact accepted amount.

```quint sir-damage.qnt +=
module SirDamageSlice {
  type ActionEntry = { id: str, argument: str, reads: Set[str], writes: Set[str] }
  type PropertyEntry = { id: str, kind: str }
  type Observation = { hitPoints: int, lastAction: str, lastAmount: int }

  pure val actions = Set(
    { id: "Initialize", argument: "none", reads: Set(), writes: Set("HitPoints", "LastAction", "LastAmount") },
    { id: "ApplyDamage", argument: "amount:int", reads: Set("HitPoints", "Amount"), writes: Set("HitPoints", "LastAction", "LastAmount") }
  )
  pure val propertyCatalogue = Set(
    { id: "NonNegativeHitPoints", kind: "invariant" },
    { id: "KnownLastAction", kind: "invariant" },
    { id: "DamageCanReachZero", kind: "reachability" }
  )

  pure def clampAtZero(value: int): int = if (value < 0) 0 else value

  var hitPoints: int
  var lastAction: str
  var lastAmount: int

  val observation: Observation = {
    hitPoints: hitPoints,
    lastAction: lastAction,
    lastAmount: lastAmount,
  }

  action init = all {
    hitPoints' = 10,
    lastAction' = "Initialize",
    lastAmount' = 0,
  }

  action applyDamage(amount: int): bool = all {
    amount >= 0,
    hitPoints' = clampAtZero(hitPoints - amount),
    lastAction' = "ApplyDamage",
    lastAmount' = amount,
  }

  action step = {
    nondet amount = 0.to(15).oneOf()
    applyDamage(amount)
  }

  val nonNegativeHitPoints = hitPoints >= 0
  val knownLastAction = actions.exists(a => a.id == lastAction)
  val damageCanReachZero = not(hitPoints == 0)
}
```

The committed witness is intentionally small and readable. It is the exact sequence the S.I.R. child
must replay through the real interpreter: initialize, apply 3, apply 20, then compare each observation.

```quint sir-damage.qnt +=
module SirDamageSliceTests {
  import SirDamageSlice.*

  run reviewedWitness =
    init
      .expect(observation == { hitPoints: 10, lastAction: "Initialize", lastAmount: 0 })
      .then(applyDamage(3))
      .expect(observation == { hitPoints: 7, lastAction: "ApplyDamage", lastAmount: 3 })
      .then(applyDamage(20))
      .expect(and {
        observation == { hitPoints: 0, lastAction: "ApplyDamage", lastAmount: 20 },
        nonNegativeHitPoints,
        knownLastAction,
      })
}
```

The independent negative control replaces `clampAtZero(hitPoints - amount)` with
`hitPoints - amount`. It must fail at the first observation after `ApplyDamage(20)` and identify the
divergent transition rather than merely report a final-state mismatch.
