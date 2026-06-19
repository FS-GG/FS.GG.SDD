---
title: "Scope: planning, optimization, and what the kernel is for"
category: Governance design
categoryindex: 7
index: 12
description: The boundary between governing and solving — why the inference kernel is a monotonic deductive *checker* (the same computational class as Cedar, generalized in domain), why planning and optimization are deliberately not native, and how the design still governs a planner or optimizer by checking its outputs at the edge.
---

# Scope: planning, optimization, and what the kernel is for

A recurring question: is this system, like Cedar, designed narrowly for
permissions/capabilities — or can it also carry **planning** (what should we do?)
and **optimization** (what is the best option?)? The short answer: it shares
Cedar's *computational paradigm*, not its *scope*, and it is built to **govern**
planning and optimization, not to **perform** them. That boundary is deliberate.

## Not permissions-shaped — adjudication-shaped

Cedar is monotonic deductive decisioning narrowed to one domain: an access
request resolves to allow/deny. This [kernel](kernel.md) keeps the *same
paradigm* — assert facts, derive verdicts, combine deterministically — but
generalizes the *domain* to any [fact vocabulary](adapters.md) and adds the
machine/agent/human [`CheckTier`](kernel.md) arbitration and the
[evidence](kernel.md) model that authorization engines do not have.

So the system is not "about permissions." It is about **adjudication**: it
answers *"is this OK, who is competent to decide it, and why?"* — a
checking/classification question. Permissions are merely one instance of that
shape; so are token drift, citation completeness, and a conserved-currency
invariant. What the kernel does **not** answer is *"what should we do?"* or
*"what is best?"* — those belong to a different computational class.

## Why planning and optimization are not native

Four design choices — each load-bearing for the
[principles](principles.md) the system exists to guarantee — put search,
sequencing, and objectives out of the kernel's reach **on purpose**:

| Design choice | Consequence for planning / optimization |
|---|---|
| **Rules are monotonic — they only *add* facts** ([kernel](kernel.md)) | Planning needs actions that *change or retract* state (the frame problem). Non-monotonic negation inside the fixed point is explicitly forbidden ([theory](theory-and-composition.md)). |
| **`Check` is applicative — no `bind`, no data-dependent sequencing** ([rule eDSL](rule-edsl.md)) | Planning is inherently sequential: the next action depends on the resulting state. The algebra is non-sequential precisely so it can be hashed/rendered/explained *without executing*. |
| **Verdicts are three-valued: `Pass` / `Fail` / `Uncertain`** ([kernel](kernel.md)) | Optimization needs a scalar or vector *objective* and a preference ordering. The kernel has no notion of "better," only "OK / not OK / unknown." |
| **The fixed point is confluent — a unique least fixed point, no search** ([theory](theory-and-composition.md)) | Planning and combinatorial optimization need *choice plus backtracking/search*. The engine converges deterministically; there is no choice operator. |

There is a fifth, quieter reason: **aggregation** (`min` / `sum` / `max` — the
doorway to an objective function) is a non-monotonic hazard outside the kernel's
safe fragment (see [open questions](open-questions.md), item 4). Even "minimize
cost" is not expressible deductively in the confluent core.

The kernel *can* do **light deductive graph reasoning** natively — reachability,
transitive closure, "what is blocked by what" over a `TaskDependsOn` or tech-web
DAG — because those are monotonic folds. What it cannot do natively is *costed*
scheduling, critical-path optimization, or any search over a space of plans.

## What it *can* do: govern planning and optimization

The important distinction is **doing the search/solve** versus
**checking/governing the result**. The kernel is built for the second, and the
design already shows the mechanism: a **deterministic probe that wraps an
external solver, planner, or simulator at the edge**, returning only a verdict to
the pure kernel.

The worked example is the **seed-sweep probe** in the
[Sojourn test design](testdesign-sojourn.md): it drives hundreds of deterministic
simulation runs — an optimization/Monte-Carlo *evaluation* — and checks whether
the resulting distribution lands inside a declared acceptance band. The
computation happens at the edge; the kernel checks the *result*. The
[`CheckTier`](kernel.md) split fits this unusually well:

> *The sweep is `Deterministic`. The band it must fall inside — "is this fair?",
> "is this fun-shaped?" — is `HumanOnly`. The diagnosis of why a sweep drifted out
> of band is `AgentReviewed`.*

Concretely, the kernel can:

- **Govern a planner** — assert the produced plan as facts, then check invariants
  over it: every step's prerequisites are met, no step skips a required stage,
  the plan closes every budget. Sojourn's `homesteadSurvivable` composite is
  exactly a plan-validity rule ("a survivable settlement must close mass, power,
  life-support, and ISRU budgets simultaneously").
- **Govern an optimizer** — run the optimization at the edge, then check that the
  solution satisfies the constraints and that its objective lands in a human-set
  band, and [taint](kernel.md) any result that rests on simulated or synthetic
  inputs.
- **Govern a process over many runs** — distributional properties ("breakthroughs
  arrive every 8–15 years") that no single run can assert become one advisory
  band check.

In every case the kernel is the **governor**, not the engine. The probe is a
black box (an `Atom` whose `Eval` calls out at the edge, or an
[`Opaque`](rule-edsl.md) node when even the result needs judgement); the search,
the objective, and the state mutation all live *outside* the pure core.

## The architectural rule

Pulling planning or optimization *into* the governance core would recreate the
exact monolith this design pivoted away from — the
[lessons](lessons.md) document is explicit that the prior "SpecFlow graph
operating system" owned route planning and release policy and became opaque and
oppressive. Keeping the kernel a small, inspectable, monotonic *checker* is the
deliberate antidote, and it preserves the one-way
[dependency direction](principles.md): governance inspects a project; it is never
on the critical path of the work it governs.

So if planning or optimization is wanted as a first-class capability, the clean
shape is the one the design already implies:

```text
planner / optimizer            (a separate engine, at the edge)
        │  produces a plan or a solution
        ▼
facts                          (the plan/solution asserted into the kernel)
        │
        ▼
kernel checks / governs        (invariants, constraint satisfaction,
                                objective-in-band, evidence taint)
```

This keeps the solver free to be as stateful, sequential, and search-heavy as it
needs to be, while the kernel stays confluent, inspectable, and explainable.

## The one reserved extension

Because the [`Check` algebra is inspectable](rule-edsl.md), the design reserves a
*future interpreter* slot ([theory](theory-and-composition.md)): a
Cedar-Analysis-style SMT fold that analyses a **rule set** rather than a single
change — "can this rule ever pass?", "is this rule shadowed?", "is the set
contradictory?". That is constraint *analysis of the rules themselves*, for
verification — still squarely on the adjudication side of the line, not a general
solver. It is the closest the kernel comes to constraint reasoning, and it
remains a checker.

## Rule of thumb

> If the question is **"is this allowed / correct / in-band / sufficiently
> evidenced, and who says so?"** it belongs *in* the kernel as a rule. If the
> question is **"which plan / which assignment / what is the best one?"** it
> belongs to an engine at the edge, whose output the kernel then governs.
