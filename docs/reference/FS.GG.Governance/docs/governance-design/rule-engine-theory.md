---
title: "The theory of the rule engine — a textbook"
category: Governance design
categoryindex: 7
index: 13
description: A self-contained textbook on the FS.GG governance rule engine — the logic and algebra, how reduction to a fixed point works, the failure modes and their mitigations, four worked adapter examples (spec-kit, Sojourn research, a composed multi-domain agent PR, and a governed optimizer), the deliberate exclusion of planning and optimization, and the pros and cons of using it to govern an agent.
---

# The theory of the rule engine

> A textbook treatment of the FS.GG governance kernel: what it computes, why that
> computation is safe, where it can go wrong, and how to read its decisions.

This document is a teaching narrative, not a reference. The reference material —
the curated `.fsi` contracts in [`src/FS.GG.Governance.Kernel/`](../../src/FS.GG.Governance.Kernel/)
and the per-topic design notes ([kernel](kernel.md), [rule eDSL](rule-edsl.md),
[routing](routing-and-modes.md), [theory](theory-and-composition.md)) — is the
ground truth. Here we connect those pieces into one story you can read end to end.

Each part ends with a short set of **exercises** you can answer from the text.

---

## Part 0 — What problem the engine solves

An agent (human or AI) proposes a change. Three questions must be answered before
that change is allowed to land:

1. **Is it OK?** — does the change satisfy the requirements that apply to it?
2. **Who is competent to decide that?** — a machine, a stochastic AI judge, or a
   person?
3. **Why?** — a decision with no legible reason is not a governance decision; it
   is an oracle.

The engine answers exactly those three questions and nothing else. It is an
**adjudicator**, not a solver. It never decides *what to do* or *what is best* —
only *whether what was proposed is acceptable, who says so, and on what grounds*.
That narrowness is the whole design (see [Part 7](#part-7--why-planning-and-optimization-are-out-of-scope)).

The shape of the system is **functional core, imperative shell**:

```text
            ┌─────────────────────────────────────────────────────────┐
            │                    THE EDGE  (F08)                       │
            │  reads git/files/clock, calls AI judges, writes logs     │
            │  — the only place effects happen —                       │
            └───────────────┬─────────────────────────▲───────────────┘
                  supplies facts                  acts on the verdict
                            │                         │
            ┌───────────────▼─────────────────────────┴───────────────┐
            │                  THE PURE KERNEL                         │
            │                                                          │
            │   facts ──▶ FixedPoint.evaluate ──▶ verdicts ──▶ Route   │
            │              (forward chaining)     (Kleene)   (light    │
            │                                                 by deflt) │
            │   total · deterministic · no I/O · no clock · no agent   │
            └──────────────────────────────────────────────────────────┘
```

Everything in this textbook lives in the lower box. The kernel is a *pure
function of values*. Sensing the world and acting on the answer is the edge's job,
and it is deliberately not modelled here.

---

## Part 1 — The atoms of the logic

### 1.1 Three-valued verdicts (Kleene logic)

The central type is not a boolean. It is a **three-valued verdict**:

```fsharp
type Verdict =
    | Pass
    | Fail of reason: string
    | Uncertain of reason: string
```

The third value is the entire point. `Uncertain` means *"a competent judge has
not yet ruled"* — it is **not** `Fail`. Conflating the two is the classic
governance bug: a question no machine can answer ("is this design tasteful?")
gets coerced to a hard failure, and either blocks honest work or gets rubber-stamped
to `Pass`. Keeping `Uncertain` as a first-class value is what lets routing later
turn it into *a review request* rather than *a block*.

The two combinators are **strong Kleene** conjunction and disjunction:

```text
  all  (∧, "all must hold")            any  (∨, "at least one")
  any Fail        ⇒ Fail               any Pass        ⇒ Pass
  else any Uncert ⇒ Uncertain          else any Uncert ⇒ Uncertain
  else            ⇒ Pass               else            ⇒ Fail

  Truth tables (P=Pass, U=Uncertain, F=Fail):

      all │ P U F          any │ P U F          negate
      ────┼──────          ────┼──────          ──────
       P  │ P U F           P  │ P P P          P → F
       U  │ U U F           U  │ P U U          U → U   (no polarity to flip)
       F  │ F F F           F  │ P U F          F → P
```

`negate` flips the pass/fail **tag** and leaves `Uncertain` alone — an unresolved
judgement has no definite polarity to invert.

Two algebraic facts make these safe to use anywhere:

- **The outcome is commutative and associative.** `all [a; b]` and `all [b; a]`
  produce the same verdict. Order of evaluation cannot change the answer.
- **Empty lists have identities.** `all [] = Pass` (vacuous truth — nothing was
  required) and `any [] = Fail ""` (nothing satisfied it). This is what makes the
  whole engine *total*: there is no list shape that has no answer.

> **Reason discipline (Hazard 2).** The *outcome* is order-free, but a naive
> implementation that reports "the first failing clause" would make the *message*
> depend on order. The kernel instead combines reasons as a **set**: split on the
> reserved `"; "` separator, de-duplicate, ordinal-sort, re-join. So the reason
> text is a function of the *set* of contributing reasons — identical under
> reordering, re-nesting, and duplication. Remember this normalization; it recurs
> verbatim three more times in the system (fence names, cache keys, route reasons).

### 1.2 The Check algebra — one value, six readings

A *rule's logic* is not a function. It is a **reified value** — a closed
discriminated union you can inspect without running it:

```fsharp
type Check<'fact> =
    | Atom    of Probe<'fact>                       // a leaf: the only thing an adapter writes
    | All     of Check<'fact> list                  // commutative ∧
    | Any     of Check<'fact> list                  // commutative ∨
    | Not     of Check<'fact>
    | Implies of Check<'fact> * Check<'fact>        // positional: a ⇒ b ≠ b ⇒ a
    | Opaque  of name: string * eval: (FactSet<'fact> -> Outcome)   // the honest escape hatch
```

The `Atom` carries a `Probe` — a small record whose *shape* (`Name`, `Reads`,
`Args`) is **declared data** and whose `Eval` is a function that is *only ever
run, never rendered or hashed*:

```fsharp
type Probe<'fact> =
    { Name: string
      Reads: ArtifactRef list                       // which artifacts it reads (drives routing + cache key)
      Args: ProbeArg list                           // ordered, declared parameters (rendered + hashed)
      Eval: FactSet<'fact> -> Outcome }             // RUN only
```

Because a `Check` is *data*, the same value can be folded **six different ways** —
and because all six fold the *one* source, they cannot drift apart. This is the
keystone of the whole design:

```text
                          ┌──────────────────┐
              eval  ◀──────┤                  ├──────▶  render
        (Verdict; run)     │                  │     (string; never runs Eval)
                           │   Check<'fact>   │
            hash  ◀────────┤   (one value)    ├──────▶  explain
     (stable cache key)    │                  │     (proof tree; verdict = eval)
                           │                  │
          reads  ◀─────────┤                  ├──────▶  isReified
   (declared artifacts)    └──────────────────┘     (any Opaque inside?)
```

| Interpreter   | Produces            | Runs `Eval`? | Guarantee |
|---------------|---------------------|:------------:|-----------|
| `eval`        | a `Verdict`         | yes          | Kleene composition over the probe outcomes |
| `render`      | a human/agent string| **no**       | the published contract IS this — cannot drift |
| `hash`        | a stable key        | **no**       | commutative nodes canonicalized; positional ones not |
| `explain`     | a proof tree        | yes          | its root verdict is *identical* to `eval` |
| `reads`       | `ArtifactRef list`  | **no**       | what to fetch + the artifact half of the cache key |
| `isReified`   | `bool`              | **no**       | false iff any `Opaque` node is present |

This "one source, many folds" property is *Data Types à la Carte* specialized to a
closed union, and the insistence that the algebra be **applicative, never monadic**
(no `bind`, no data-dependent sequencing) is what keeps it analysable without
running it (Capriotti & Kaposi; see [theory](theory-and-composition.md)). You can
only build a `Check` whose structure is fixed in advance, which is precisely why
`render`/`hash`/`reads` can fold it without ever touching a probe.

> **The honest escape hatch.** `Opaque` exists for the genuinely irreducible
> check. It carries a name but no inspectable structure: `isReified` returns
> `false`, so the rule builder **refuses** to call it `Deterministic` — it is
> forced to `AgentReviewed` or `HumanOnly`. Opacity is *visible*, never silent.
> An opaque check that pretends to be machine-decidable is unrepresentable.

### 1.3 Rules, tiers, and severity — three orthogonal axes

A `Check` is just logic. A **rule** wraps it with three independent declarations:

```fsharp
type CheckRule<'fact> =
    { Id: RuleId
      Tier: CheckTier        // WHO decides
      Spec: SpecSource       // which requirement it traces to (provenance)
      Severity: Severity     // HOW BADLY a failure matters
      Check: Check<'fact>    // the logic
      Question: string option }
```

The two governance axes are **orthogonal**, and keeping them orthogonal is what
prevents the engine from becoming oppressive:

```text
                    SEVERITY  (does failure stop you?)
                    Advisory                Blocking
                 ┌──────────────────────┬──────────────────────┐
   Deterministic │ machine reports       │ machine blocks (rare,│
                 │ ("contrast is low")   │ opt-in, at the gate) │
   ──────────────┼──────────────────────┼──────────────────────┤
   AgentReviewed │ AI judge advises      │ AI judge gates       │
   (WHO          │ ("plan may miss FR-3")│ (gate on a verdict)  │
    decides)     ├──────────────────────┼──────────────────────┤
   HumanOnly     │ surfaced for a person │ escalates regardless │
                 │ to weigh in           │ (never auto-decides) │
                 └──────────────────────┴──────────────────────┘
```

- **`CheckTier` says who is competent.** `Deterministic` requires a fully reified
  check (the builder enforces it). `AgentReviewed` consults a content-hash cache
  and, on a miss, *emits a review request as data* — it never calls an AI itself.
  `HumanOnly` always escalates and never decides.
- **`Severity` says whether failure stops you.** It defaults to `Advisory`. Most
  of a catalog is advisory: it reports and moves on. `Blocking` is opt-in, rare,
  and the entire blocking set must be listable at a glance — a long blocking list
  is a design smell.

> **The AI judge never runs inside the kernel.** An `AgentReviewed` rule produces
> a `NeedsReview` request (a content-hash cache key plus the prompt) *as a value*.
> The edge dispatches it, gets a verdict, and records it; the next run finds a
> cache **hit** and short-circuits. The judge's identity (`{ModelId; Version}`) is
> folded into the cache key, so a verdict frozen by one model is never silently
> reused after the model changes. The kernel stays pure; the stochastic step is
> quarantined at the edge and *memoized by content*.

### Exercises 1

1. Why is `any [] = Fail ""` rather than `Pass`? What property would break if it
   were `Pass`?
2. A check `All [contrast; spacing]` fails. Give two reason strings a *naive*
   engine might emit depending on order, and the single string the kernel emits.
3. Why can `render` and `hash` fold a `Check` without a `FactSet`, but `eval`
   cannot?
4. You have an irreducible "is this prose persuasive?" check. Which `CheckTier`s
   are legal for it, and which one does the builder forbid? Why?

---

## Part 2 — Reduction to a fixed point

### 2.1 Facts, rules, and forward chaining

The kernel's reasoning core is a **monotonic forward-chaining reasoner** — the
Datalog model. It works over *facts* and *rules*:

```fsharp
type Rule<'fact> =
    { Id: RuleId
      Description: string                                   // = Check.render (drift-proof)
      Apply: FactSet<'fact> -> FactAssertion<'fact> list }  // current facts ⟶ newly asserted facts
```

A rule is an ordinary typed F# function. There is **no external rule language**.
The defining constraint is **monotonicity**: a rule may only *add* facts, never
retract or mutate them. Each derived fact carries a `ProvenanceStep` — the rule
that fired and the input facts it consumed — so every conclusion is traceable to
its grounds (this is *reason maintenance*).

### 2.2 The reduction, step by step

`FixedPoint.evaluate` reduces a set of supplied facts under a set of rules to the
**least fixed point** — the smallest fact set closed under the rules. Here is the
actual algorithm ([`Kernel.fs`](../../src/FS.GG.Governance.Kernel/Kernel.fs)),
drawn as a loop:

```text
   supplied facts
        │
        ▼
   ┌─────────────────────────────────────────────────────────────┐
   │ (1) NORMALIZE: identify(value) → FactId, dedup, empty        │
   │     provenance for asserted facts. `identify` is the SOLE     │
   │     identity authority.                                       │
   └──────────────────────────────┬──────────────────────────────┘
                                  ▼
        ╔══════════════════ ROUND ════════════════════╗
        ║  current := snapshot()   (sorted by FactId)  ║  ◀──────┐
        ║                                              ║         │
        ║  (2) apply EVERY rule to the SAME snapshot   ║         │
        ║      (a fact derived this round is invisible ║         │
        ║       to other rules until the next round)   ║         │
        ║                                              ║         │
        ║  (3a) re-identify each produced fact;        ║         │
        ║       drop ids already known                 ║         │
        ║  (3b) group new candidates by FactId; pick   ║         │
        ║       the winner by total order (FactId,     ║         │
        ║       RuleId) — deterministic tie-break      ║         │
        ║                                              ║         │
        ║  (4) selected = [] ?                          ║         │
        ║        yes ─▶ QUIESCENCE (stop)              ║         │
        ║        no  ─▶ commit, rounds += 1 ───────────╫─────────┘
        ╚══════════════════════════════════════════════╝
                                  │
                                  ▼
   (5) EMIT: { Facts = snapshot (sorted by FactId); Rounds }
```

Three design choices make this reduction **confluent** — the result is independent
of the order rules are written or evaluated in:

1. **Synchronous rounds.** Every rule sees the *same immutable snapshot* in a
   round. A fact derived this round is invisible to its peers until the next
   round. This removes intra-round order sensitivity entirely.
2. **`identify` is the sole identity authority.** The kernel re-identifies *every*
   fact (supplied and derived), so two facts with the same identity collapse to
   one — dedup is structural, not positional.
3. **A total tie-break.** When two rules produce the same fact in a round, the
   winner is chosen by the total order on `(FactId, RuleId)`, so even the
   *provenance* of a fact is order-independent, not just its presence.

### 2.3 Why it terminates and why the answer is unique

- **Monotonic + bounded ⇒ terminates.** Each productive round adds at least one
  new fact and never removes one. Over a bounded fact space the known set strictly
  grows until no rule produces anything new (quiescence). An empty round is the
  stopping condition and is *not* counted in `Rounds`.
- **Monotonic ⇒ unique least fixed point.** This is the Datalog guarantee: a
  monotonic rule set has exactly one least fixed point, reached regardless of
  evaluation order. The `Rounds` count is reported for observability (how many
  waves of derivation it took), but the *final fact set* is an order-invariant.

```text
   Round 0:  { A, B }                       (supplied)
   Round 1:  { A, B, C }        C ⟵ rule r1 from {A}
   Round 2:  { A, B, C, D, E }  D ⟵ r2 from {B,C};  E ⟵ r3 from {C}
   Round 3:  { A, B, C, D, E }  nothing new  ──▶  quiescence, Rounds = 2
```

### 2.4 The second fixed point: evidence taint

There is a *second* least-fixed-point computation in the kernel, structurally
identical in spirit: the **synthetic-taint closure** over the evidence graph.

Every governed node carries an `EvidenceState` — one of six values tracking *how
trustworthy the evidence is*, orthogonal to whether a verdict holds:

```text
   [ ] Pending   [X] Real   [S] Synthetic   [F] Failed   [-] Skipped   [S*] AutoSynthetic
                              (root cause)                               (COMPUTED only)
```

`Synthetic` (`[S]`) is the *declared* root cause of a taint — "done, but only on
placeholder evidence." `AutoSynthetic` (`[S*]`) is **never declared**; it is
*computed* by the closure: a `Real` node that rests — directly or transitively —
on a synthetic node is downgraded to `[S*]`.

```text
   effective(t) over a DAG:

        Real ──depends──▶ Real ──depends──▶ Synthetic[S]
         │                  │                   (root)
         ▼                  ▼
      becomes [S*]      becomes [S*]        ◀── taint flows UP the
      (rests on a       (rests on a              dependency edges,
       tainted dep)      tainted dep)            transitively
```

The closure is a least fixed point for the same reason the main reasoner is: the
dependency graph is a **DAG** (the `EvidenceGraph` constructor *rejects cycles*, so
the closure provably terminates), the taint rule only ever downgrades `Real → [S*]`
(monotone in one direction), and `Pending`/`Failed`/`Skipped` are *never* upgraded.
The crucial property: it is a **pure function of the current states and edges,
carrying no hidden history**. Re-declare a `Synthetic` root as `Real`, recompute,
and the taint *clears everywhere it had flowed* — no other bookkeeping. Diamonds
(a node tainted by many paths) taint exactly once; the result is idempotent.

### Exercises 2

1. The reasoner reports `Rounds = 0`. What does that tell you about the rules and
   the supplied facts?
2. Two rules `r1` (`RuleId "a"`) and `r2` (`RuleId "z"`) both derive the same fact
   in the same round. Whose `ProvenanceStep` survives, and why is that choice made?
3. A node is `Real` but transitively depends on one `Synthetic` node through three
   intermediate `Real` nodes. What is its effective state? What happens if the
   `Synthetic` node is re-declared `Real` and the closure re-runs?
4. Why must the evidence graph be a DAG for `effective` to be total?

---

## Part 3 — Routing: from verdicts to a decision

The reasoner tells you *what holds*. **Routing** turns that into *what a change
needs* — light by default, always explained. Three orthogonal dials:

```text
   STAKES        ── is this change high-stakes?      Routine | Fenced name
   (of the change)   raised ONLY by a matching fence

   SEVERITY      ── does failure stop you?           Advisory | Blocking
   (of a rule)       a property authored on the rule

   RUN MODE      ── where in the lifecycle are we?   Sandbox | Inner | Gate
   (of the run)      decides WHEN a stake is enforced
```

### 3.1 Light by default — stakes by precedence

A change is `Routine` (no gates, regardless of mode) **unless** it positively trips
a declared *fence* — a named classifier:

```fsharp
type Fence<'change> = { Name: string; Trips: 'change -> bool }
type Stakes = Routine | Fenced of name: string
```

The critical subtlety (and a corrected hazard — see [Part 6](#part-6--the-failure-modes-and-their-mitigations)):
stakes are combined by **deterministic precedence, never by first match.** If *any*
fence trips, the change is `Fenced`, carrying the **set** of tripped fence names —
de-duplicated, ordinal-sorted, `"; "`-joined (the same normalization as verdict
reasons). So the result is *identical under any permutation of the fence list*. An
early design sketch used `List.tryFind` ("first match wins") — that is the
firewall/iptables order-dependence bug, and the shipped engine deliberately
strengthens it away.

```text
   change ──▶ run every fence's Trips predicate
                       │
            any trips? ─── no ──▶  Routine   (light: no gates, any mode)
                       │
                      yes
                       ▼
            Fenced "<sorted ∪ of tripped names>"   (forbid trumps permit)
```

### 3.2 Run modes — the honest escape hatch

```fsharp
type RunMode = Sandbox | Inner | Gate
```

A blocking-severity requirement on a `Fenced` change is a **blocking gate iff all
three line up**:

```text
   blocking gate  ⟺  Severity = Blocking  ∧  Stakes = Fenced  ∧  RunMode = Gate
   everything else  ⟶  Advisory
```

`Sandbox` and `Inner` are advisory-only: you can develop, debug, and throw a draft
around with zero friction. The property that makes this safe — and that the prior
art's disclosure-only flag never provided — is that **`Gate` recomputes from the
diff against the base branch and ignores any local mode.** You can develop without
the machinery; you physically *cannot land* an un-governed state, because the
boundary re-runs the gates independently.

Note that **stakes classification is identical across all three modes** — run mode
changes *enforcement*, not *classification*. The same change is `Fenced` in
`Sandbox` and in `Gate`; only whether the fence *bites* differs.

### 3.3 The route, and why it always explains itself

```fsharp
type Route =
    { Stakes: Stakes
      Advisory: ContractEntry list
      Blocking: ContractEntry list      // the short, filterable subset
      Reason: string }                  // ALWAYS non-empty
```

`route` computes the stakes, folds the applicable rules into drift-proof
`ContractEntry` requirements **reusing the published-contract fold** (so each
entry's `Statement` *is* `Check.render` of the rule's check — it cannot drift from
what is enforced), and partitions them. Every route carries a **non-empty reason**;
"no reason" is *unrepresentable*, because the reason is constructed from the stakes,
the mode, and the rendered checks. The light, no-gates case is the normal, *loud*
case — not silence:

```text
   $ route            # 3 files changed, none on a fenced surface
   light — no gates (advisory only). Routine: no declared fence tripped.

   $ route            # change touches a fenced public surface, mode = Gate
   gate: public-surface          (blocking)
     ← stakes: Fenced "public-api"
     ← check:  surface-matches(GeneratedTokenSurface, TokenDocument)
   advisory: contrast-policy
     ← check:  contrast-meets(Ant, ThemeSurface)
```

The full pipeline, end to end:

```text
  1. classify the change      ▶ 'change   (the adapter supplies the notion of "change")
  2. match against fences      ▶ Routine | Fenced name        (light by default)
  3. select applicable rules   ▶ rules whose Check.reads intersect the change   (adapter's job)
  4. fold to the contract      ▶ ContractEntry list           (Statement = Check.render)
  5. partition by the 3-dial test ▶ Blocking gates vs Advisory
  6. render with a reason for every entry
```

Steps 2 and 5 are the two levers that keep the system light: **nothing is heavy
unless a fence matched, and nothing blocks unless a rule opted into blocking.**

### Exercises 3

1. A change trips fences `["b-surface"; "a-surface"]` in that order. What is the
   `Fenced` name? Would reordering the fence list change the route? Why not?
2. A `Blocking` rule applies to a `Fenced` change in `Inner` mode. Does it block?
   Where in the three-part test does it fall out?
3. Why is it impossible to produce a `Route` whose `Reason` is empty?

---

## Part 4 — Four worked examples

These four are chosen to climb in complexity: a single-domain adapter, a
data-rich invariant surface, a *composed* multi-domain agent PR, and a governed
*optimizer* (which previews [Part 7](#part-7--why-planning-and-optimization-are-out-of-scope)).

### Example A — the Spec Kit adapter (single domain)

Spec Kit is the *harness* (the authored work loop `specify → plan → tasks →
implement → merge`); the engine is the *observer* attached to it. Governance never
*writes* `spec.md`; it reads artifacts, asserts facts, and runs rules.

**The fact vocabulary** (the adapter's only domain-specific code):

```fsharp
type Phase = Constitution | Specify | Clarify | Plan | Tasks | Analyze | Implement | Merge
type SpecKitFact =
    | PhaseReached     of Phase                       // supplied from .specify/feature.json
    | ArtifactPresent  of SpecKitArtifact
    | TaskState        of taskId: string * EvidenceState
    | TaskDependsOn    of taskId: string * dep: string
    | ConstitutionArea of area: string * filled: bool
```

The stateless kernel handles the stateful lifecycle by treating the **current
phase as a supplied fact**. Rules guard on it: `whenPhase Plan check` contributes
only once the feature reaches `Plan`. The `tasks.deps.yml` topology becomes
`TaskDependsOn` facts, and *the evidence/taint closure runs the kernel over them* —
`EvidenceGraph` is a derivation, not a bespoke engine. That reuse is the payoff of
a domain-neutral core.

**The catalog mixes all three tiers**, mostly advisory:

| Spec Kit check | Tier | Default severity |
|---|---|---|
| `tasks.md` ↔ `tasks.deps.yml` consistent, acyclic, ids resolve | Deterministic | Advisory |
| Constitution areas filled, non-placeholder | Deterministic | Advisory → blocking at merge |
| Evidence not synthetic (no `[S]`/`[S*]` reaching main) | Deterministic | Blocking, **at merge only** |
| Does `plan.md` address every requirement in `spec.md`? | AgentReviewed | Advisory |
| Is the feature in scope / worth doing? | HumanOnly | — |

```fsharp
let planSatisfiesSpec =
    rule "plan-satisfies-spec" AgentReviewed Spec
        (whenPhase Plan (Opaque ("plan-covers-spec", fun _ -> Unknown "judgement")))
    |> asking "Does plan.md address every requirement in spec.md? List gaps."
    // advisory: REPORTS gaps during plan; never blocks planning
```

**Run modes map to phases.** The inner loop is all `Inner` (advisory); **merge is
the single fence** that flips to `Gate`, recomputes from base, and lets blocking
rules bite:

```text
   constitution Inner   author the fences + severities; advisory completeness
   specify      Sandbox draft freely; advisory well-formedness
   plan         Inner   advisory plan-covers-spec; anticipate fences
   tasks        Inner   advisory graph well-formedness
   analyze      Inner   the WHOLE catalog runs — as a report, not a gate
   merge        Gate    recompute from base; blocking rules enforced or merge refused
```

This is the cure for the old monolith's `analyze`-pass opacity: each check renders
to a sentence and explains itself, instead of one pass emitting a wall of output.
**The lesson of Example A:** a whole development workflow is governed by *facts +
a rule catalog + a phase-to-mode mapping* — no engine changes, only vocabulary.

### Example B — Sojourn's research system (a data-rich invariant surface)

Sojourn is a deterministic Rust 4X game. Its research system (Understanding Levels,
TRL maturation, seeding, breakthroughs, a "science tide," reliability) is the
richest invariant surface in the game, and today its rules live as scattered Rust
tests and implicit clamps. The adapter reifies them.

This example shows the engine handling logic that is *not* a simple boolean per
artifact:

- **Graph folds.** "No technology matures past TRL *n* until its prerequisites
  have" is a reachability/closure check over the tech web — exactly the kind of
  monotonic deductive graph reasoning the kernel does natively (the same shape as
  `TaskDependsOn`).
- **Property laws.** "Understanding Levels are monotone non-decreasing within a
  run" and "the science-tide ordering is deterministic" are *universally
  quantified* properties, encoded as reified checks and exercised with FsCheck.
- **Distributional / seed-sweep bands.** "Breakthroughs arrive every 8–15 years"
  cannot be asserted by any single run. The adapter uses a **deterministic probe
  that drives hundreds of simulation runs at the edge** and checks whether the
  resulting distribution lands inside a *declared acceptance band*.

That last one is the bridge to Part 7. The `CheckTier` split fits it unusually well:

> *The sweep is `Deterministic`. The band it must fall inside — "is this fair?",
> "is this fun-shaped?" — is `HumanOnly`. The diagnosis of why a sweep drifted out
> of band is `AgentReviewed`.*

**The lesson of Example B:** even rich, statistical, graph-structured invariants
reduce to *reified checks over asserted facts*. The heavy computation (the sweep)
happens at the edge; the kernel governs the *result*. What the adapter buys over
scattered Rust tests is a single explainable contract: every invariant renders to
a sentence, carries provenance, and is enforced uniformly.

### Example C — a composed multi-domain agent PR (cross-domain coupling)

Now the hard case. An AI coding agent opens a pull request that simultaneously
edits **design tokens**, a **Spec Kit feature**, and **production F# source**.
Three independent adapters must govern one change *together*.

The composition is the **coproduct of the fact types** — *Data Types à la Carte*:

```fsharp
type ProjectFact =
    | Design   of DesignSystemFact
    | SpecKit  of SpecKitFact
    | Software of SoftwareFact
// each adapter's rules are "lifted" into ProjectFact by a single hand-written injection
```

Each interpreter (`eval`/`render`/`hash`/`explain`) for the coproduct is just the
case-split of the per-domain interpreters — a textbook catamorphism. Adding an
adapter is a central edit to `ProjectFact` (a deliberate, reviewable
composition root); adding an *interpreter* stays trivial. That is the closed-union
trade: we give up open third-party extensibility to get one place to review.

The genuinely interesting rules **span adapters** — and this is exactly where
naive systems become non-confluent glue. The discipline is that cross-domain
coupling is expressed **only** two ways, never as "run A then B":

1. **In the AST, via `Implies` over the coproduct's facts:**

   ```fsharp
   // "a task that touches the design surface must carry a recorded review"
   let designTouchNeedsReview =
       rule "design-touch-review" Deterministic CrossDomain
           (taskTouches "design/**" ==> hasRecordedReview)   // positional ⇒
   ```

2. **At combine time, via fixed order-independent precedence** — the Cedar model:
   a blocking `forbid` always wins; default allow-unless-fenced. This keeps the
   merged verdict deterministic and independent of rule order.

The route over the composed change:

```text
   agent PR ──▶ fences: ["design-surface" trips, "public-api" trips, "spec-merge" trips]
            ──▶ Stakes = Fenced "design-surface; public-api; spec-merge"   (sorted ∪)
            ──▶ applicable rules selected by reads-intersection across all 3 domains
            ──▶ partition (mode = Gate):
                  BLOCKING:
                    public-surface     ← surface-matches(GeneratedTokenSurface, TokenDocument)
                    evidence-real      ← no [S]/[S*] reaches main
                    design-touch-review← taskTouches(design/**) ⇒ hasRecordedReview
                  ADVISORY:
                    contrast-policy, plan-covers-spec (AgentReviewed), ...
```

Two subtle correctness properties this example exercises:

- **The cross-domain `Implies` is positional but order-free overall.** `a ⇒ b ≠
  b ⇒ a` for hashing (correct — implication is directional), yet the *route* is
  still permutation-invariant in the fence and rule lists, because partitioning
  depends on stakes/mode, not position.
- **The blocking set stays short.** Even across three domains, the gate lists
  three rules — because *advisory is the default* and the selection is
  reads-bounded. A long blocking list would be the design smell that says a domain
  over-fenced.

**The lesson of Example C:** multi-domain governance is the *coproduct of
vocabularies* plus *two disciplined cross-domain combinators*. There is no "glue
layer" — the glue is `Implies` and deterministic precedence, both confluent.

### Example D — governing an optimizer (the edge solver)

The most advanced pattern, and the one most often misunderstood. A team wants to
govern an **autonomous deployment optimizer**: an agent that searches over rollout
configurations (instance counts, canary percentages, region order) to minimize cost
subject to an SLO. *The engine does not — and must not — perform that search.* It
**governs the result.**

The shape:

```text
   optimizer / planner          (a separate engine, at the edge — stateful,
        │  searches the space     sequential, search-heavy: NOT in the kernel)
        ▼
   facts                        (the chosen plan asserted into the kernel:
        │                        PlanStep, BudgetClosed, CanaryPct, RestsOnSim…)
        ▼
   kernel checks / governs      (invariants · constraint satisfaction ·
                                 objective-in-band · evidence taint)
```

The rule catalog over the *asserted plan*:

```fsharp
// Deterministic: every step's prerequisites are met, no required stage is skipped
let planWellFormed =
    rule "plan-prereqs" Deterministic Spec
        (allOf [ everyStepPrereqMet; noStageSkipped; everyBudgetClosed ]) |> blocking

// Deterministic at the edge: the cost objective lands in a HUMAN-set band
let costInBand =
    rule "cost-in-band" Deterministic Spec
        (Atom (probe "cost-within" reads args (fun facts -> /* read the edge result */)))

// HumanOnly: is the band itself acceptable? — a person owns "what is good"
let bandIsFair =
    rule "band-acceptable" HumanOnly Spec (Opaque ("band-fair", fun _ -> Unknown "judgement"))

// AgentReviewed: if the sweep drifted out of band, diagnose WHY
let driftDiagnosis =
    rule "drift-why" AgentReviewed Spec (Opaque ("diagnose-drift", fun _ -> Unknown "judgement"))
    |> asking "The optimizer's cost left the band. What changed to cause it?"
```

And the crucial honesty hook — **evidence taint**: if the optimizer's inputs were
*simulated* rather than measured, the plan node is declared `Synthetic`, and the
taint closure downgrades every `Real` conclusion that rests on it to
`AutoSynthetic`. A plan that *looks* validated but rests on a synthetic load model
is automatically flagged, and `evidence-real` (blocking at the gate) refuses it.

```text
   chosen-plan (Real) ──depends──▶ load-model (Synthetic [S])
        │                              (simulated, not measured)
        ▼
   chosen-plan effective = [S*]  ──▶  evidence-real rule fails at the gate
```

The `CheckTier` split is the whole elegance here: **the search is at the edge, the
constraint check is `Deterministic`, the objective-in-band is `Deterministic`
against a `HumanOnly` band, and the drift diagnosis is `AgentReviewed`.** Each
question goes to whoever is competent to answer it.

**The lesson of Example D:** you *can* govern planning and optimization — fully,
with provenance and taint — without the kernel ever performing a search. The solver
stays free to be as stateful and search-heavy as it needs; the kernel stays
confluent, inspectable, and explainable. This is the worked motivation for Part 7.

---

## Part 5 — Reading a decision (provenance and explanation)

Because every derived fact carries a `ProvenanceStep` and every check can
`explain` itself into a proof tree whose root verdict *equals* `eval`, a decision
is never an oracle. You can always walk from the top-line verdict down to the
atomic probe outcomes and back up to the rule and the requirement it traces to:

```text
   Route.Blocking[0] = public-surface (blocking)
        │  Statement = "surface-matches(GeneratedTokenSurface, TokenDocument)"   (= Check.render)
        ▼
   explain(facts, check) =
        All [verdict = Fail "tokens drifted"]
          └─ Atom "surface-matches" → Unmet "GeneratedTokenSurface ≠ TokenDocument"  → Fail
        │
        ▼
   ProvenanceStep { Rule = "public-surface"; Inputs = [tokenDocFactId]; Note = <rendered check> }
        │
        ▼
   SpecSource { Document = "design-system"; Section = "token-currency" }   ← the requirement
```

The `Statement` you see in the published contract is literally `Check.render` of
the same value `eval` ran — so what is *advertised* and what is *enforced* cannot
diverge. This drift-proofness is not a convention; it is structural, because there
is only one source.

---

## Part 6 — The failure modes and their mitigations

The engine is order-independent *by intent*, and most layers are confluent *by
construction*. But there is a short, known list of hazards — the places where a
careless implementation would leak order-dependence or non-termination. A textbook
must teach the failure modes, not just the happy path.

| # | Hazard | What goes wrong | Mitigation (shipped) |
|---|--------|-----------------|----------------------|
| 1 | **Negation-as-failure** | A rule that fires on the *absence* of a fact is non-monotonic; whether it fires depends on what was derived first → non-confluent. | **Stratify.** Facts that other rules negate-check are *supplied* before evaluation (stratum 0), never *derived* inside the same fixed point. (`Check.Not` is NOT this hazard — it negates an already-evaluated sub-verdict, which is total.) |
| 2 | **Provenance / reason order** | The fact *set* is confluent, but reporting "the first failing clause" makes the *message* order-dependent. | Combine reasons as a **set**: split on `"; "`, dedup, ordinal-sort, re-join. The reason is a function of the set of components. |
| 3 | **Hashing commutative nodes** | `hash(All [a;b])` ≠ `hash(All [b;a])` would cause spurious cache misses and needless re-review. | **Canonicalize** commutative nodes (sort member hashes); keep positional nodes (`Implies`, ordered `Args`) positional. |
| 4 | **Dedup / `identify`** | A non-injective `identify` makes "keep which duplicate?" order-dependent. | An **injective** `identify`; keep the first under a total order; assert collisions are equal. |
| 5 | **Positional rules** | "First matching rule decides" (the iptables failure mode) makes order load-bearing. | **Deterministic precedence**, never first-match: a blocking result always wins; default allow-unless-fenced. This is exactly the corrected `stakesOf` (Part 3.1). |

Beyond these five, three whole-system non-termination / unboundedness risks are
foreclosed by construction:

- **Cyclic evidence graphs** → the `EvidenceGraph` constructor *rejects cycles*
  (returns `Cycle`/`UnknownNode`/`AutoSyntheticDeclared`), so the taint closure is
  always a terminating DAG fold.
- **Non-monotonic rules in the fixed point** → forbidden by precondition; the
  reasoner is only guaranteed to terminate for monotonic rules over a bounded
  space. (This is documented, not runtime-enforced — it is the one obligation on
  rule authors.)
- **An `Opaque` check masquerading as machine-decidable** → the rule builder
  refuses `Deterministic` for any check containing an `Opaque` node
  (`OpaqueCannotBeDeterministic`), so opacity can never silently claim
  reproducibility.

> **The two disciplines that keep the whole system confluent:** (1) keep the rule
> set **stratified** — no negation over still-being-derived facts; (2)
> **canonicalize** hashing and provenance for commutative nodes. All *intended*
> ordering is quarantined in the phase state machine, which is explicitly
> sequential and *emits facts*; the check algebra over those facts stays an
> order-free fold.

### Exercises 6

1. Classify each as a real hazard or a false alarm: (a) `Check.Not` over a probe
   verdict; (b) a rule that fires when `hasRecordedReview` is *absent*; (c)
   `hash(Any [x; y])` vs `hash(Any [y; x])`.
2. The taint closure never loops. Which constructor guarantee makes that true?
3. Why is the monotonicity precondition documented but *not* runtime-enforced?
   What would enforcing it cost?

---

## Part 7 — Why planning and optimization are out of scope

A recurring question: this looks like a general reasoning engine — why can it not
*plan* (decide what to do) or *optimize* (decide what is best)? The answer is that
the engine shares Cedar's *computational paradigm* (monotonic deductive
decisioning) but is built to **govern** planning and optimization, not to
**perform** them. Four load-bearing design choices put search, sequencing, and
objectives out of reach **on purpose**:

```text
   ┌────────────────────────────────────┬───────────────────────────────────────┐
   │ Design choice (kernel)             │ Why it forecloses planning/optimization │
   ├────────────────────────────────────┼───────────────────────────────────────┤
   │ Rules are MONOTONIC (add-only)     │ Planning needs actions that CHANGE or   │
   │                                    │ RETRACT state — the frame problem.      │
   ├────────────────────────────────────┼───────────────────────────────────────┤
   │ Check is APPLICATIVE (no bind)     │ Planning is sequential: the next action │
   │                                    │ depends on the resulting state.         │
   ├────────────────────────────────────┼───────────────────────────────────────┤
   │ Verdicts are 3-valued (P/F/U)      │ Optimization needs a scalar OBJECTIVE   │
   │                                    │ and a preference order. There is no     │
   │                                    │ notion of "better," only OK/not/unknown.│
   ├────────────────────────────────────┼───────────────────────────────────────┤
   │ The fixed point is CONFLUENT       │ Planning/optimization need CHOICE +     │
   │ (unique least FP, no search)       │ backtracking. There is no choice op.    │
   └────────────────────────────────────┴───────────────────────────────────────┘
```

There is a fifth, quieter reason: **aggregation** (`min`/`sum`/`max` — the doorway
to an objective function) is a non-monotonic hazard outside the safe fragment. Even
"minimize cost" is not expressible deductively in the confluent core.

What the kernel *can* do natively is **light deductive graph reasoning** —
reachability, transitive closure, "what is blocked by what" over a `TaskDependsOn`
or tech-web DAG — because those are monotonic folds. What it cannot do is *costed*
scheduling, critical-path optimization, or any search over a space of plans.

### How to incorporate planning/optimization *safely*

The mechanism is the one Example D already demonstrated: a **deterministic probe
(or `Opaque` node) that wraps an external solver at the edge and returns only a
verdict** to the pure kernel.

```text
   planner / optimizer   (separate engine, at the edge: stateful, sequential, search-heavy)
        │  produces a plan or a solution
        ▼
   facts                 (the plan/solution asserted into the kernel)
        │
        ▼
   kernel checks/governs  (invariants · constraint satisfaction · objective-in-band · taint)
```

Concretely, the kernel can:

- **Govern a planner** — assert the produced plan as facts, then check invariants:
  every step's prerequisites met, no required stage skipped, every budget closed.
- **Govern an optimizer** — run the optimization at the edge, then check the
  solution satisfies the constraints and its objective lands in a *human-set band*,
  and **taint** any result resting on simulated/synthetic inputs.
- **Govern a process over many runs** — distributional properties no single run can
  assert (the seed-sweep band) become one advisory check.

In every case the kernel is the **governor**, not the engine. Pulling search into
the core would recreate the exact monolith this design pivoted away from (the prior
"SpecFlow graph operating system" owned route planning and became opaque and
oppressive). Keeping the kernel a small, inspectable, monotonic *checker* is the
deliberate antidote — and it preserves the one-way dependency: **governance
inspects a project; it is never on the critical path of the work it governs.**

> **Rule of thumb.** If the question is *"is this allowed / correct / in-band /
> sufficiently evidenced, and who says so?"* it belongs **in** the kernel as a
> rule. If the question is *"which plan / which assignment / what is best?"* it
> belongs to an engine **at the edge**, whose output the kernel then governs.

**The one reserved extension.** Because the `Check` algebra is inspectable, the
design reserves a *future interpreter* slot: a Cedar-Analysis-style SMT fold that
analyses a **rule set** rather than a single change — "can this rule ever pass?",
"is this rule shadowed?", "is the set contradictory?". That is constraint
*analysis of the rules themselves*, for verification — still squarely a checker,
not a general solver. It is one more algebra over the same `Check`.

---

## Part 8 — Governing an agent: pros and cons

The engine's reason for existing is to sit between an autonomous agent and the
artifacts it produces. Here is the honest balance sheet.

### The case for it

- **Light by default, so it is not resented.** An unclassified change escalates to
  *nothing*. Heavy machinery requires a positive match against a small, named,
  fenced surface. The agent (and its human supervisor) can iterate at full speed;
  only the genuinely high-stakes surfaces pull in gates. This is the single most
  important property — the prior art failed precisely because it was default-deny.
- **Explainable by construction.** Every verdict carries provenance; every check
  renders to a sentence; the published contract *is* the rendered selector. An
  agent's reviewer can always answer "why was this blocked?" in one line. There is
  no opaque "the system says no."
- **Honest about uncertainty.** Three-valued logic means a question no machine can
  answer becomes a *review request*, not a coerced pass or fail. The agent is never
  silently rubber-stamped on a judgement call.
- **Honest about evidence.** The synthetic-taint closure means work built on
  placeholder/simulated data *cannot* masquerade as validated — the taint flows
  transitively and is unbypassable at the gate. This is uniquely valuable against
  an agent, which is very good at producing plausible-looking synthetic evidence.
- **The right judge for each question.** `CheckTier` routes machine-decidable
  checks to a machine, judgement calls to an AI (cached by content, so cheap and
  reproducible), and irreducible value calls to a human — and *records who decided*.
- **Unbypassable boundary with a frictionless inner loop.** `Sandbox`/`Inner` let
  the agent develop without friction; `Gate` recomputes from base and ignores local
  mode, so an un-governed state physically cannot land. You get both speed and a
  hard floor.
- **Domain-neutral and composable.** The same kernel governs code, research,
  essays, and game invariants; multi-domain projects compose by coproduct. One
  engine, many vocabularies, no per-domain engine to maintain.

### The costs and risks

- **It governs outputs, not intentions.** The engine checks the *artifact* an agent
  produced. It cannot tell you the agent reasoned badly if the output happens to
  satisfy every rule. A determined or misaligned agent that learns the rule set can
  produce rule-satisfying-but-undesirable work; the rules are a *floor*, not a
  guarantee of quality.
- **The catalog is the bottleneck.** The engine is only as good as its rules.
  Writing reified checks, fences, and severities is real, ongoing work, and an
  under-specified catalog gives false comfort. The blocking set in particular must
  be curated — the "listable at a glance" discipline is a constraint you must hold.
- **The monotonicity obligation is on the author.** It is documented, not
  runtime-enforced. A rule author who derives a negated or aggregated fact *inside*
  the fixed point breaks confluence, and nothing stops them at compile time. This
  is a deliberate trade (enforcing it is expensive) but it is a real sharp edge.
- **AI-judged checks inherit the judge's fallibility.** An `AgentReviewed` verdict
  is a stochastic model's opinion, frozen and cached. The cache key includes the
  judge identity (so a model change invalidates it), but a *wrong* cached verdict
  is reused until the inputs or the judge change. Mitigation: keep `AgentReviewed`
  advisory unless the cost of a wrong block is low, and prefer `HumanOnly` for the
  truly consequential value calls.
- **It is not a planner or a fixer.** The engine says "this is wrong, here is why."
  It does not propose the fix or the better plan. That is correct (Part 7), but it
  means the agent or a human still has to *act* on the verdict — the engine closes
  no loop by itself.
- **Fences can be mis-drawn.** Light-by-default is only safe if the high-stakes
  surfaces are *actually* fenced. An un-fenced surface that should have been fenced
  is a silent gap. The constitution-as-the-dial pattern concentrates this risk in
  one reviewable place, but it is still a place that can be wrong.

### The honest summary

> The engine is an **excellent floor and an honest mirror**: it makes the
> high-stakes surfaces unbypassable, keeps everything else frictionless, never lies
> about evidence or uncertainty, and always explains itself. It is **not a ceiling
> on quality, not a substitute for good rules, and not a closed loop** — it
> adjudicates, it does not solve, and it governs outputs, not intent. Used for what
> it is, it is the antidote to both the oppressive-monolith failure mode and the
> rubber-stamp failure mode. Used as a guarantee of agent *alignment* or *quality*,
> it will quietly disappoint.

### Exercises 8

1. An agent produces work that passes every blocking rule but is obviously
   low-quality. Is that a bug in the engine? What is the engine actually promising?
2. Give a concrete case where you would keep an `AgentReviewed` check *advisory*
   rather than promote it to `Blocking`, and explain the risk you are avoiding.
3. Why does light-by-default shift the dominant risk from "too much friction" to
   "a surface that should have been fenced wasn't"? Where is that risk concentrated?

---

## Appendix — the engine in one diagram

```text
   ADAPTER (domain-specific: the ONLY thing you write per domain)
     │  fact vocabulary  ·  probes (Atom leaves)  ·  fences  ·  rule catalog
     ▼
   ┌──────────────────────────────── KERNEL (domain-neutral, pure) ────────────────────────────────┐
   │                                                                                                 │
   │  facts ─▶ FixedPoint.evaluate ─────────────────────────────────▶ derived facts + provenance    │
   │            (monotonic forward chaining → unique least fixed point)                               │
   │                                                                                                 │
   │  Check<'fact> ──┬─ eval ──▶ Verdict (Kleene P/U/F)                                               │
   │   (reified,     ├─ render ─▶ statement  ─────┐                                                   │
   │    applicative) ├─ hash ───▶ cache key       │ (one source ⇒ no drift)                           │
   │                 ├─ explain ▶ proof tree      │                                                   │
   │                 ├─ reads ──▶ ArtifactRefs    │                                                   │
   │                 └─ isReified ▶ bool          │                                                   │
   │                                              ▼                                                   │
   │  EvidenceGraph ─▶ effective (taint closure over a DAG → least fixed point)                       │
   │                                                                                                 │
   │  Route = stakesOf(fences) × severity × runmode ─▶ { Stakes; Advisory; Blocking; Reason }         │
   │           (light by default · forbid trumps permit · always explained)                          │
   └─────────────────────────────────────────────────────────────────────────────────────────────────┘
     ▲                                                                                              │
     │ supplies facts                                                          acts on the verdict │
   EDGE (F08): senses git/files/clock, dispatches AI judges, records verdicts, logs disclosures
```

## Further reading

- [The inference kernel](kernel.md) — facts, rules, fixed point, provenance,
  `CheckTier`, evidence model.
- [The rule eDSL](rule-edsl.md) — the `Check` algebra, the six interpreters, the
  `Opaque` hatch, the bridge.
- [Routing, severity, and run modes](routing-and-modes.md) — light-by-default
  routing and the merge boundary.
- [Theory and composition](theory-and-composition.md) — Data Types à la Carte,
  applicative inspectability, the confluence hazards, the prior art (Cedar, OPA).
- [Scope: planning, optimization, and what the kernel is for](planning-and-optimization.md)
  — the adjudication-not-solving boundary in full.
- [Spec-driven development in the system](speckit-in-the-system.md) and
  [Adapter proposal: Sojourn's research system](adapter-sojourn-research.md) — the
  two real adapters Examples A and B summarize.
</content>
</invoke>
