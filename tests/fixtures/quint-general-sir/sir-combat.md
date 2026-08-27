# S.I.R. combat rules in Quint

This document is the reviewer-facing authority for the complete sixteen-rule combat registry. Its
embedded Quint deliberately uses several modeling scales: catalogue data for rule identity, pure
functions for fixed-point arithmetic, a contract for an external line-of-sight algorithm, and actions
for state-changing consequences. The generated `sir-combat.qnt` file is a projection, not another
source to edit.

The model stores fixed-point numbers as their signed Q4 raw integer. Thus `1.0` is `10000`, rifle
damage `25` is `250000`, and a retention ratio of `0.8` is `8000`. This mirrors
`SIR.Domain.FixedPoint` and makes rounding and saturation visible rather than relying on host floats.

`COMBAT-TRACE-002` is intentionally a contract boundary. Quint relates valid visible and total sample
counts to a trace ratio, but `FS.GG.Game.Core.Los.lineOfSightBy` remains the registered supercover
implementation. Copying that pathfinding algorithm here would create two authorities.

```quint sir-combat.qnt +=
module SirCombat {
  type RuleEntry = {
    id: str,
    kind: str,
    dependencies: Set[str],
    reads: Set[str],
    effects: Set[str],
    events: List[str],
  }
  type AlgorithmEntry = {
    id: str,
    implementation: str,
    fingerprint: str,
    inputs: Set[str],
    result: str,
    explanationFields: List[str],
  }
  type PropertyEntry = { id: str, kind: str, subjects: Set[str] }
  type Wound = NoWound | MinorWound | MajorWound
  type CombatState = {
    health: int,
    suppression: int,
    coverIntegrity: int,
    coverBlocking: bool,
    incapacitated: bool,
  }
  type AttackInput = {
    hasTargetFootprint: bool,
    baseDamageRaw: int,
    visibleSamples: int,
    totalSamples: int,
    rangeCells: int,
    armorRetentionRaw: int,
    suppressionDelta: int,
    directAttack: bool,
    projectileBlocking: bool,
    attackerFaction: str,
    targetFaction: str,
    eventId: str,
  }
  type Observation = {
    lastAction: str,
    damage: int,
    preparationRaw: int,
    traceRaw: int,
    retentionRaw: int,
    wound: Wound,
    contact: bool,
    suppressionDelta: int,
    coverDamage: int,
    destroyed: bool,
    stopsProjectile: bool,
    explanationOrder: List[str],
    eventId: str,
    attackerFaction: str,
    targetFaction: str,
  }

  pure val SCALE = 10000
  pure val INT32_MIN = -2147483648
  pure val INT32_MAX = 2147483647
  pure val rifleDamageRaw = 250000
  pure val humanArmorRetentionRaw = SCALE
  pure val rangeSlopeRaw = 1000

  pure val ruleCatalogue = Set(
    { id: "CONTENT-WEAPON-RIFLE-001", kind: "fact", dependencies: Set(), reads: Set(), effects: Set(), events: List() },
    { id: "CONTENT-BODY-HUMAN-001", kind: "fact", dependencies: Set(), reads: Set(), effects: Set(), events: List() },
    { id: "COMBAT-ENGAGEMENT-001", kind: "formula", dependencies: Set(), reads: Set("range"), effects: Set(), events: List() },
    { id: "COMBAT-TRACE-002", kind: "algorithm", dependencies: Set(), reads: Set("visible", "total"), effects: Set(), events: List() },
    { id: "COMBAT-ARMOR-004", kind: "formula", dependencies: Set(), reads: Set("retention"), effects: Set(), events: List() },
    { id: "COMBAT-DAMAGE-001", kind: "formula", dependencies: Set("CONTENT-WEAPON-RIFLE-001", "COMBAT-TRACE-002", "COMBAT-ARMOR-004"), reads: Set("baseDamage", "trace", "retention"), effects: Set(), events: List() },
    { id: "COMBAT-COLLISION-001", kind: "transition", dependencies: Set("COMBAT-TRACE-002"), reads: Set("trace.outcome", "trace.crossings"), effects: Set("projectile.contact"), events: List("ContactResolved") },
    { id: "COMBAT-COVER-003", kind: "transition", dependencies: Set("COMBAT-COLLISION-001"), reads: Set("cover.integrity", "cover.projectileBlocking"), effects: Set("cover.integrity"), events: List("CoverDamaged") },
    { id: "COMBAT-PENETRATION-001", kind: "transition", dependencies: Set("COMBAT-COVER-003", "COMBAT-ARMOR-004"), reads: Set("armor.rating", "weapon.penetration"), effects: Set("damage.retention"), events: List("ArmorResolved") },
    { id: "COMBAT-HEALTH-001", kind: "transition", dependencies: Set("COMBAT-DAMAGE-001"), reads: Set("target.health"), effects: Set("target.health"), events: List("HealthChanged") },
    { id: "COMBAT-WOUND-001", kind: "transition", dependencies: Set("COMBAT-HEALTH-001"), reads: Set("target.health", "damage"), effects: Set("target.wounds", "target.incapacitated"), events: List("WoundApplied", "Incapacitated") },
    { id: "COMBAT-SUPPRESSION-001", kind: "transition", dependencies: Set("COMBAT-COLLISION-001"), reads: Set("target.suppression", "weapon.suppression"), effects: Set("target.suppression"), events: List("SuppressionChanged") },
    { id: "COMBAT-SUPPRESSION-RECOVERY-001", kind: "transition", dependencies: Set("COMBAT-SUPPRESSION-001"), reads: Set("target.suppression"), effects: Set("target.suppression"), events: List("SuppressionChanged") },
    { id: "COMBAT-COLLATERAL-001", kind: "transition", dependencies: Set("COMBAT-COLLISION-001"), reads: Set("target.faction", "attacker.faction"), effects: Set("target.health", "target.suppression"), events: List("AttackResolved") },
    { id: "COMBAT-COVER-DESTRUCTION-001", kind: "transition", dependencies: Set("COMBAT-COVER-003"), reads: Set("cover.integrity"), effects: Set("cover.projectileBlocking"), events: List("CoverDestroyed") },
    { id: "COMBAT-ATTACK-RESOLUTION-001", kind: "transition", dependencies: Set("COMBAT-ENGAGEMENT-001", "COMBAT-COLLISION-001", "COMBAT-COVER-003", "COMBAT-PENETRATION-001", "COMBAT-DAMAGE-001", "COMBAT-WOUND-001", "COMBAT-SUPPRESSION-001", "COMBAT-COLLATERAL-001"), reads: Set("attacker.cell", "target.footprint", "weapon", "cover", "armor", "target.health", "target.suppression"), effects: Set("cover.integrity", "target.health", "target.wounds", "target.incapacitated", "target.suppression"), events: List("AttackResolved", "CoverDestroyed") }
  )

  pure val traceAlgorithm = {
    id: "COMBAT-TRACE-002",
    implementation: "FS.GG.Game.Core.Los.lineOfSightBy",
    fingerprint: "FS.GG.Game.Core@0.13.0:Los.lineOfSightBy:Supercover",
    inputs: Set("visible:int:samples", "total:int:samples"),
    result: "fixedPoint:ratio",
    explanationFields: List("visibleSamples", "totalSamples", "lineMode"),
  }

  pure def saturateInt32(value: int): int =
    if (value < INT32_MIN) INT32_MIN
    else if (value > INT32_MAX) INT32_MAX
    else value

  pure def absolute(value: int): int = if (value < 0) -value else value
  pure def minimum(left: int, right: int): int = if (left < right) left else right
  pure def maximum(left: int, right: int): int = if (left > right) left else right

  pure def divideRoundedAwayFromZero(numerator: int, denominator: int): int = {
    val quotient = numerator / denominator
    val remainder = numerator % denominator
    if (absolute(remainder) * 2 < absolute(denominator)) quotient
    else if ((numerator < 0) != (denominator < 0)) quotient - 1
    else quotient + 1
  }

  pure def fromRatio(numerator: int, denominator: int): int =
    saturateInt32(divideRoundedAwayFromZero(numerator * SCALE, denominator))

  pure def addFixed(left: int, right: int): int = saturateInt32(left + right)

  pure def multiplyFixed(left: int, right: int): int =
    saturateInt32(divideRoundedAwayFromZero(left * right, SCALE))

  pure def bounded100(value: int): int = maximum(0, minimum(100, value))
  pure def retainedEffect(retentionRaw: int): int = maximum(0, minimum(SCALE, retentionRaw))
  pure def preparationRaw(rangeCells: int): int = addFixed(SCALE, multiplyFixed(fromRatio(rangeCells, 1), rangeSlopeRaw))
  pure def validTrace(visible: int, total: int): bool = and { total > 0, visible >= 0, visible <= total }
  pure def traceRaw(visible: int, total: int): int = fromRatio(visible, total)
  pure def expectedDamageRaw(baseDamageRaw: int, trace: int, retention: int): int =
    multiplyFixed(multiplyFixed(baseDamageRaw, trace), retainedEffect(retention))
  pure def roundedDamage(rawDamage: int): int = (rawDamage + SCALE / 2) / SCALE
  pure def woundForDamage(damage: int): Wound =
    if (damage >= 50) MajorWound else if (damage >= 25) MinorWound else NoWound

  pure val consequenceExplanationOrder = List(
    "COMBAT-COLLISION-001",
    "COMBAT-ENGAGEMENT-001",
    "COMBAT-TRACE-002",
    "COMBAT-ARMOR-004",
    "COMBAT-DAMAGE-001",
    "COMBAT-COVER-003",
    "COMBAT-PENETRATION-001",
    "COMBAT-HEALTH-001",
    "COMBAT-WOUND-001",
    "COMBAT-SUPPRESSION-001",
    "COMBAT-COLLATERAL-001"
  )

  pure val propertyCatalogue = Set(
    { id: "SixteenRulesDeclared", kind: "invariant", subjects: Set("RuleCatalogue") },
    { id: "BoundedCombatState", kind: "invariant", subjects: Set("Health", "Suppression", "CoverIntegrity") },
    { id: "IncapacityMatchesHealth", kind: "invariant", subjects: Set("Health", "Incapacitated") },
    { id: "DestroyedCoverIsPermeable", kind: "invariant", subjects: Set("CoverIntegrity", "CoverBlocking") },
    { id: "ValidTraceObservation", kind: "invariant", subjects: Set("Trace") },
    { id: "SuppressionRequiresDamage", kind: "invariant", subjects: Set("Damage", "Suppression") },
    { id: "FactionNeutralCollateral", kind: "example", subjects: Set("AttackerFaction", "TargetFaction", "Damage", "Suppression") }
  )

  pure def validAttack(input: AttackInput): bool = and {
    input.hasTargetFootprint,
    validTrace(input.visibleSamples, input.totalSamples),
  }

  pure def damageForAttack(input: AttackInput): int =
    roundedDamage(expectedDamageRaw(
      input.baseDamageRaw,
      traceRaw(input.visibleSamples, input.totalSamples),
      input.armorRetentionRaw
    ))

  pure def suppressionForDamage(damage: int, requestedDelta: int): int =
    if (damage > 0) maximum(0, requestedDelta) else 0

  pure def nextConsequences(current: CombatState, input: AttackInput): CombatState = {
    val damage = damageForAttack(input)
    val nextHealth = bounded100(current.health - damage)
    val appliedSuppression = suppressionForDamage(damage, input.suppressionDelta)
    {
      health: nextHealth,
      suppression: bounded100(current.suppression + appliedSuppression),
      coverIntegrity: current.coverIntegrity,
      coverBlocking: current.coverBlocking,
      incapacitated: nextHealth == 0,
    }
  }

  pure def consequenceObservation(input: AttackInput): Observation = {
    val damage = damageForAttack(input)
    val retained = retainedEffect(input.armorRetentionRaw)
    {
      lastAction: "ResolveConsequences",
      damage: damage,
      preparationRaw: preparationRaw(input.rangeCells),
      traceRaw: traceRaw(input.visibleSamples, input.totalSamples),
      retentionRaw: retained,
      wound: woundForDamage(damage),
      contact: damage > 0,
      suppressionDelta: suppressionForDamage(damage, input.suppressionDelta),
      coverDamage: 0,
      destroyed: false,
      stopsProjectile: false,
      explanationOrder: consequenceExplanationOrder,
      eventId: input.eventId,
      attackerFaction: input.attackerFaction,
      targetFaction: input.targetFaction,
    }
  }

  pure def coverDamage(baseDamage: int): int = maximum(1, baseDamage / 2)

  pure def nextCoverImpact(current: CombatState, baseDamage: int): CombatState = {
    val remaining = bounded100(current.coverIntegrity - coverDamage(baseDamage))
    {
      health: current.health,
      suppression: current.suppression,
      coverIntegrity: remaining,
      coverBlocking: if (remaining == 0) false else current.coverBlocking,
      incapacitated: current.incapacitated,
    }
  }

  pure def coverObservation(
    current: CombatState,
    baseDamage: int,
    projectileBlocking: bool,
    directAttack: bool,
    eventId: str
  ): Observation = {
    val remaining = bounded100(current.coverIntegrity - coverDamage(baseDamage))
    {
      lastAction: "ResolveCoverImpact",
      damage: 0,
      preparationRaw: 0,
      traceRaw: 0,
      retentionRaw: 0,
      wound: NoWound,
      contact: false,
      suppressionDelta: 0,
      coverDamage: coverDamage(baseDamage),
      destroyed: remaining == 0,
      stopsProjectile: directAttack and projectileBlocking,
      explanationOrder: List("COMBAT-COVER-DESTRUCTION-001", "COMBAT-COVER-003"),
      eventId: eventId,
      attackerFaction: "",
      targetFaction: "",
    }
  }

  pure def recoveredSuppression(currentSuppression: int): int = minimum(5, maximum(0, currentSuppression))

  pure def nextRecovery(current: CombatState): CombatState = {
    val remaining = current.suppression - recoveredSuppression(current.suppression)
    {
      health: current.health,
      suppression: remaining,
      coverIntegrity: current.coverIntegrity,
      coverBlocking: current.coverBlocking,
      incapacitated: current.incapacitated,
    }
  }

  pure def recoveryObservation(current: CombatState, eventId: str): Observation = {
    val recovered = recoveredSuppression(current.suppression)
    {
      lastAction: "ResolveRecovery",
      damage: 0,
      preparationRaw: 0,
      traceRaw: 0,
      retentionRaw: 0,
      wound: NoWound,
      contact: false,
      suppressionDelta: -recovered,
      coverDamage: 0,
      destroyed: false,
      stopsProjectile: false,
      explanationOrder: if (recovered == 0) List() else List("COMBAT-SUPPRESSION-RECOVERY-001"),
      eventId: eventId,
      attackerFaction: "",
      targetFaction: "",
    }
  }

  pure val representativeAttack: AttackInput = {
    hasTargetFootprint: true,
    baseDamageRaw: rifleDamageRaw,
    visibleSamples: 10,
    totalSamples: 10,
    rangeCells: 3,
    armorRetentionRaw: 8000,
    suppressionDelta: 12,
    directAttack: true,
    projectileBlocking: true,
    attackerFaction: "Blue",
    targetFaction: "Red",
    eventId: "attack:representative",
  }

  pure val missedAttack: AttackInput = {
    ...representativeAttack,
    visibleSamples: 0,
    suppressionDelta: 12,
    eventId: "attack:miss",
  }

  pure def fullDamageAttack(damage: int, eventId: str): AttackInput = {
    ...representativeAttack,
    baseDamageRaw: damage * SCALE,
    armorRetentionRaw: SCALE,
    eventId: eventId,
  }

  pure val alliedAttack: AttackInput = {
    ...representativeAttack,
    targetFaction: "Blue",
    eventId: "collateral:allies",
  }

  pure val initialCombat: CombatState = {
    health: 100,
    suppression: 0,
    coverIntegrity: 100,
    coverBlocking: true,
    incapacitated: false,
  }

  var combat: CombatState
  var last: Observation

  action init = all {
    combat' = initialCombat,
    last' = {
      lastAction: "Initialize",
      damage: 0,
      preparationRaw: 0,
      traceRaw: 0,
      retentionRaw: 0,
      wound: NoWound,
      contact: false,
      suppressionDelta: 0,
      coverDamage: 0,
      destroyed: false,
      stopsProjectile: false,
      explanationOrder: List(),
      eventId: "initialize",
      attackerFaction: "",
      targetFaction: "",
    },
  }

  action resolveConsequences(input: AttackInput): bool = all {
    validAttack(input),
    combat' = nextConsequences(combat, input),
    last' = consequenceObservation(input),
  }

  action resolveCoverImpact(
    baseDamage: int,
    projectileBlocking: bool,
    directAttack: bool,
    eventId: str
  ): bool = all {
    combat' = nextCoverImpact(combat, baseDamage),
    last' = coverObservation(combat, baseDamage, projectileBlocking, directAttack, eventId),
  }

  action resolveRecovery(eventId: str): bool = all {
    combat' = nextRecovery(combat),
    last' = recoveryObservation(combat, eventId),
  }

  action step = any {
    resolveConsequences(representativeAttack),
    resolveConsequences(missedAttack),
    resolveCoverImpact(25, true, true, "cover:sample"),
    resolveRecovery("recovery:sample"),
  }

  val sixteenRulesDeclared = ruleCatalogue.size() == 16
  val boundedCombatState = and {
    combat.health >= 0, combat.health <= 100,
    combat.suppression >= 0, combat.suppression <= 100,
    combat.coverIntegrity >= 0, combat.coverIntegrity <= 100,
  }
  val incapacityMatchesHealth = combat.incapacitated == (combat.health == 0)
  val destroyedCoverIsPermeable = (combat.coverIntegrity == 0) implies not(combat.coverBlocking)
  val validTraceObservation =
    (last.lastAction == "ResolveConsequences") implies and { last.traceRaw >= 0, last.traceRaw <= SCALE }
  val suppressionRequiresDamage =
    (last.lastAction == "ResolveConsequences" and last.damage <= 0) implies last.suppressionDelta == 0
  val factionNeutralCollateral =
    nextConsequences(initialCombat, alliedAttack) == nextConsequences(initialCombat, representativeAttack)
}
```

The examples below are executable reviews of boundary behavior. They use separate `run` declarations
so a failing scenario names the behavioral subject rather than merely reporting a long trace.

```quint sir-combat.qnt +=
module SirCombatTests {
  import SirCombat.*

  run representativeDamageIsTwenty =
    init
      .then(resolveConsequences(representativeAttack))
      .expect(and {
        last.damage == 20,
        last.preparationRaw == 13000,
        last.traceRaw == SCALE,
        last.retentionRaw == 8000,
        combat.health == 80,
        combat.suppression == 12,
        last.explanationOrder == consequenceExplanationOrder,
        sixteenRulesDeclared,
        boundedCombatState,
        incapacityMatchesHealth,
      })

  run woundThresholdsAreExact =
    init
      .then(resolveConsequences(fullDamageAttack(24, "wound:24")))
      .expect(last.wound == NoWound)
      .then(resolveConsequences(fullDamageAttack(25, "wound:25")))
      .expect(last.wound == MinorWound)
      .then(resolveConsequences(fullDamageAttack(50, "wound:50")))
      .expect(and { last.wound == MajorWound, combat.health == 1 })

  run zeroHealthMeansIncapacitated =
    init
      .then(resolveConsequences(fullDamageAttack(100, "health:zero")))
      .expect(and { combat.health == 0, combat.incapacitated, incapacityMatchesHealth })

  run suppressionNeedsPositiveDamageAndRecoversFive =
    init
      .then(resolveConsequences(missedAttack))
      .expect(and { last.damage == 0, last.suppressionDelta == 0, combat.suppression == 0 })
      .then(resolveConsequences(representativeAttack))
      .expect(and { last.damage == 20, combat.suppression == 12 })
      .then(resolveRecovery("recovery:target"))
      .expect(and {
        combat.suppression == 7,
        last.suppressionDelta == -5,
        last.explanationOrder == List("COMBAT-SUPPRESSION-RECOVERY-001"),
      })

  run destroyingCoverConsumesCurrentCollision =
    init
      .then(resolveCoverImpact(250, true, true, "cover:destroy"))
      .expect(and {
        last.coverDamage == 125,
        combat.coverIntegrity == 0,
        last.destroyed,
        last.stopsProjectile,
        destroyedCoverIsPermeable,
      })

  run collateralOutcomeIgnoresFaction = {
    init
      .then(resolveConsequences(alliedAttack))
      .expect(and {
        last.damage == damageForAttack(representativeAttack),
        combat.health == 80,
        combat.suppression == 12,
        factionNeutralCollateral,
      })
  }
}
```

The model makes two granularity decisions explicit. Focused rules remain queryable pure definitions,
while `ResolveConsequences` is atomic because the production interpreter exposes only its completed
result. Cover impact and suppression recovery stay separate actions because they are separate runtime
entry points. This gives us useful formal structure without inventing observable intermediate states.
