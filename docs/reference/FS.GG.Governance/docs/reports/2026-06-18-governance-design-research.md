# Governance-design research — offline + online review

**Date:** 2026-06-18
**Scope:** `docs/governance-design/` (11 documents)
**Method:** Offline reading + synthesis of all design docs, then four parallel
web-research agents that adversarially verified the design's theory/prior-art
claims and contextualised it against current (2025–2026) practice. Every
external claim is cited; primary sources were preferred.

---

## Executive summary

The governance design is a **domain-neutral inference kernel** (typed facts +
monotonic forward-chaining rules to a fixed point, with provenance) plus four
pillars: a **`CheckTier`** arbitration model (machine / agent / human), a
**reified, applicative `Check` algebra** with four interpreters
(`eval`/`render`/`hash`/`explain`), **light-by-default routing**
(`Stakes`/`Severity`/`RunMode` with an unbypassable merge fence), and **domain
adapters** (design-system, Spec Kit, two Sojourn worked examples). It is
design-only; nothing is implemented.

Adversarial verification **largely confirms the design's unusually rigorous
theoretical footing**, with three substantive findings:

1. Its core architecture (rules-as-data + multiple interpreters + deterministic
   combination) is directly validated by **Cedar** (peer-reviewed, OOPSLA 2024).
2. The largest *operational* exposure is the reality of LLM judges — most
   actionably, the proposed agent-verdict **cache key omits the judge model /
   prompt version**, so cached verdicts would silently rot on a model upgrade.
3. The "confluent by construction" claim is **conditional**: it holds only inside
   the stratified-negation, aggregation-safe, function-free fragment; the docs
   understate non-stratifiable negation and never mention aggregation.

---

## 1. What it is (offline synthesis)

A reusable library a project **embeds**, never a platform it runs on. The
dependency rule is one-way: governance may inspect a project; a project must
never require governance.

- **Kernel** — typed `'fact` assertions + `Rule`s that only *add* facts,
  evaluated by forward chaining to a fixed point, each derived fact carrying
  provenance (the rule + inputs that justified it).
- **`CheckTier` arbitration** — every rule declares *who is competent to decide
  it*: `Deterministic` (machine), `AgentReviewed` (an LLM; verdict frozen as
  evidence keyed by a content hash so it is only re-consulted when inputs
  change), `HumanOnly` (escalate). Verdicts are three-valued
  (`Pass`/`Fail`/`Uncertain`).
- **Reified `Check` algebra** — a closed, *applicative* DU
  (`Atom/All/Any/Not/Implies` + an `Opaque` escape hatch), folded by four
  interpreters so verdict, contract text, cache key, and proof tree cannot
  drift. `Opaque` is barred from `Deterministic` tier.
- **Light + advisory routing** — `Stakes` (Routine unless a change matches a
  named *fence*), `Severity` (Advisory by default; Blocking opt-in and rare),
  `RunMode` (`Sandbox`/`Inner`/`Gate`), with the **merge fence** recomputing
  from base so a local sandbox cannot land un-governed work.
- **Adapters** — design-system (Ant), Spec Kit (phases-as-facts,
  constitution-as-dial), and two deep Sojourn worked examples (a deterministic
  Rust 4X game) demonstrating cross-language, cross-domain generality.

The design is explicitly shaped against a prior failure (a monolithic "SpecFlow
graph OS" that was default-deny, all-blocking, and opaque).

## 2. Claim verification (online)

| Claim | Verdict | Note |
|---|---|---|
| **Cedar** = policy-as-data + separate engine + multiple interpreters (eval + SMT analyzer) + `forbid`-trumps-`permit` / default-deny | ✅ Accurate | Strongest external validation. Cedar Analysis (SMT, CVC5) open-sourced Jun 2025 answers exactly "can this pass / is it shadowed / contradictory." |
| **OPA/Rego** = Datalog-derived, declarative, separates what/how | ✅ Accurate | But "multiple interpreters folded + deterministic combination" is a **Cedar** property, *not* OPA — the doc over-attributes. |
| Cedar/OPA are "our design at industrial scale" | ⚠️ Overstated | Both are **runtime allow/deny authorization**; FS.GG is **build/merge-time** governance over work artifacts. Architecture analogy holds; use case differs. |
| **Data Types à la Carte**: coproduct of signatures, `:<:` injection, interpreters-as-catamorphisms, Expression-Problem trade-off | ✅ Accurate | "Conceptual, not isomorphic; closed sum → hand-written lifting boilerplate" caveat is fair. |
| **Free Applicative**: applicative structure statically analysable without executing; monadic isn't | ✅ Accurate | Paper hedges "*certain kinds of* static analysis"; doc's flat phrasing drops the hedge (minor). Its own "too absolute" concession is correct. |
| Monotonic Datalog → unique least fixed point, terminating, order-independent | ✅ Accurate | Textbook ("Datalog guarantee"). |
| Negation-as-failure is the confluence hazard; **stratify** to fix | ✅ Accurate but **understated** | Stratified negation fixes *stratifiable* programs only; cyclic negation has no stratified model. **Aggregation** is a separate non-monotonic hazard never mentioned. "Confluent by construction" = "confluent *within the stratified, aggregation-safe, function-free fragment*." |
| Kleene 3-valued AND/OR commutative & associative | ✅ Accurate | min/max over F<U<T. |
| Per-fact provenance ≈ reason maintenance / why-provenance | ✅ Accurate | Caveat: why-provenance for *recursive* Datalog can blow up; cheap only for non-recursive. The "collect all failing reasons, sort by RuleId" fix is sound. |
| Rete = classical production-rule matching | ✅ Accurate | |

**Net:** the footing is real and correctly cited. Substantive gaps: (a)
over-claiming OPA, (b) runtime-vs-build-time domain mismatch with the policy
engines, (c) confluence being *conditional* (non-stratifiable negation +
aggregation unaddressed).

## 3. Current-landscape fit (online)

- **LLM-as-judge:** the field handles judge non-determinism *statistically*
  (run N times, report mean±σ, test both orderings). FS.GG instead **freezes one
  verdict and content-addresses it** — essentially Bazel-style memoization
  applied to judgments. Principled and novel as a *governance* unit, but it bakes
  in single-sample judge noise.
- **Caching mechanics — most actionable finding:** eval tools (Promptfoo,
  Inspect AI) key their cache on a digest of the *model request* — **model id +
  prompt + config + inputs**. FS.GG's proposed key is **rule hash + artifacts
  read**, which **omits the judge model and prompt version**. A silent
  model/prompt upgrade would reuse stale verdicts on identical inputs.
- **Guardrails (NeMo, Guardrails AI):** *runtime* input/output filters for live
  agents — orthogonal to FS.GG's artifact/merge-time framing, which is a genuine
  gap-filler, not a competitor.
- **Tiered autonomy / HITL:** `Deterministic/AgentReviewed/HumanOnly` maps
  cleanly onto established L1–L5 autonomy and 3-tier approval-gate models, applied
  to *rule adjudication* rather than runtime actions.
- **AI-in-CI / Spec Kit:** AI review as a required merge check + "policy-as-code
  merge gates in version-controlled config" is the consolidating industry
  pattern. Spec Kit's own `constitution.md` is only a *prompt-context* device, so
  FS.GG enforcing it with a **recomputing merge boundary** fills a real gap.

## 4. Critical assessment

**Strengths (well-founded):**
- Reified-rules-as-data + multiple-interpreters + deterministic-combination is
  directly validated by Cedar (peer-reviewed).
- Applicative-only `Check` for inspectability is theoretically correct.
- Light/advisory/escape-hatch is a coherent, evidence-based reaction to the prior
  failure and aligns with the industry move away from noisy hard-blocking AI
  signals.
- The `CheckTier` machine/agent/human split is a clean, established framing.

**Gaps / risks (priority order) — tracked as open questions and issues:**
1. **Cache key omits the judge** — include model id + version + prompt hash, or
   cached `AgentReviewed` verdicts silently rot on model upgrades. *(Highest-value, cheap fix.)*
2. **Frozen single verdict = frozen noise** — consider N-run aggregation or a
   confidence threshold before freezing.
3. **Prompt injection of the agent reviewer** — governed artifacts (code, specs,
   essays) are attacker-/author-controlled input to the judge; an injected
   instruction can flip a verdict.
4. **Confluence is conditional** — document non-stratifiable negation and
   aggregation as out-of-fragment hazards, not solved problems.
5. **Gaming + meta-validation debt** — advisory-by-default invites ignoring
   signals; `AgentReviewed` rules need ongoing judge-vs-human calibration; per-
   review cost/latency quietly undercuts "light by default."
6. **OPA over-claim / domain mismatch** — trim the prose so the policy-engine
   analogy is architectural, not "same problem at industrial scale."

**Bottom line:** intellectually honest and unusually well-grounded — most prior-
art claims survive adversarial verification, and the core architecture has strong
industrial precedent (Cedar). Real exposure is the **operational reality of LLM
judges** (cache invalidation on model drift, single-sample noise, prompt
injection) and a couple of **understated formal edge cases** (non-stratifiable
negation, aggregation).

## Consolidated sources

**Policy engines**
- Cedar (OOPSLA 2024) — <https://arxiv.org/abs/2403.04651>
- Cedar Analysis (SMT tools, Jun 2025) — <https://aws.amazon.com/blogs/opensource/introducing-cedar-analysis-open-source-tools-for-verifying-authorization-policies/>
- Cedar authorization (forbid-overrides / default-deny) — <https://docs.cedarpolicy.com/auth/authorization.html>
- OPA / Rego policy language — <https://www.openpolicyagent.org/docs/policy-language>

**PL theory**
- Swierstra, *Data Types à la Carte*, JFP 2008 — <https://www.cs.tufts.edu/~nr/cs257/archive/wouter-swierstra/DataTypesALaCarte.pdf>
- Capriotti & Kaposi, *Free Applicative Functors*, MSFP 2014 — <https://arxiv.org/abs/1403.0749>
- Wadler on the Expression Problem — <https://wadler.blogspot.com/2008/02/data-types-la-carte.html>

**Logic / inference**
- Datalog & forward chaining (CMU, Pfenning) — <https://www.cs.cmu.edu/~fp/courses/lp/lectures/26-datalog.pdf>
- Stratified negation (Wisconsin CS784) — <https://pages.cs.wisc.edu/~paris/cs784-s17/lectures/lecture9.pdf>
- Three-valued (Kleene) logic — <https://en.wikipedia.org/wiki/Three-valued_logic>
- Provenance survey (Cheney et al.) — <https://homepages.inf.ed.ac.uk/jcheney/publications/provdbsurvey.pdf>
- Rete algorithm — <https://en.wikipedia.org/wiki/Rete_algorithm>

**Current practice (LLM judges, guardrails, HITL, AI-in-CI)**
- LLM-as-judge (Braintrust) — <https://www.braintrust.dev/articles/what-is-llm-as-a-judge>
- Promptfoo caching — <https://www.promptfoo.dev/docs/configuration/caching/>
- Inspect AI caching — <https://inspect.aisi.org.uk/caching.html>
- AI guardrails overview — <https://generalanalysis.com/guides/best-ai-guardrails>
- NeMo Guardrails — <https://developer.nvidia.com/nemo-guardrails>
- Levels of autonomy (CSA) — <https://cloudsecurityalliance.org/blog/2026/01/28/levels-of-autonomy>
- AI code review in CI/CD (Augment) — <https://www.augmentcode.com/guides/ai-code-review-ci-cd-pipeline>
- GitHub Spec Kit — <https://github.com/github/spec-kit>

**Source caveat.** Primary-source claims above are from authoritative URLs. A few
arXiv IDs the landscape agent cited for LLM-judge *risks* (e.g. "2606.15474",
"2604.16790") could not be independently confirmed and may be inaccurate — but
the underlying risks (judge-model drift, position/sample bias, reviewer prompt
injection) are well-established and corroborated by the reputable sources listed.

---

## Appendix A — raw findings: policy engines (Cedar & OPA/Rego)

> Cedar (OOPSLA 2024) reifies policies as data, evaluates them with a separate
> engine, and runs multiple interpreters — a runtime authorizer, a type
> validator, and an SMT analyzer (Cedar Analysis, CVC5, open-sourced Jun 2025)
> answering "can this pass / is it shadowed / contradictory / more permissive" —
> combined deterministically (any satisfied `forbid` → Deny; else any satisfied
> `permit` → Allow; otherwise default Deny). All four sub-claims **Accurate**.
> Rego "was inspired by Datalog and extends it to support structured document
> models" and is declarative ("focus on *what* queries should return rather than
> *how*") — **Accurate**. The overall analogy is **partly accurate**: strongly
> true for Cedar; for OPA the "multiple interpreters folded + deterministic
> combination" property does **not** hold (OPA's decision logic is whatever the
> author writes; no built-in SMT analyzer, no fixed combinator). Both are
> *runtime allow/deny authorization* systems, not build/merge-time governance —
> the structural analogy holds, the use case does not, and "our design at
> industrial scale" is borderline. Strongest legitimate claim: Cedar is a
> peer-reviewed instance of exactly "rules-as-data, multiple interpreters,
> deterministic fold."

## Appendix B — raw findings: PL theory (DTalC & Free Applicative)

> **Data Types à la Carte** (Swierstra, JFP 2008): composition via coproduct of
> *functor* signatures (`:+:`), automatic injection via the `:<:` class
> (`inj`/`prj`, type-signature-driven resolution), interpreters as catamorphisms
> whose coproduct algebra is the case-split of component algebras, and a canonical
> Expression-Problem solution keeping the case set *open* at the cost of typeclass
> machinery — **all Accurate**. The doc's characterization of its own
> `ProjectFact` as a *closed* sum with hand-written injections (trading open
> extensibility for a reviewable root + real-but-small lifting boilerplate) is a
> **sound, fair** summary. **Free Applicative** (Capriotti & Kaposi, MSFP 2014):
> applicative structure is "fixed a priori… makes it possible to perform certain
> kinds of static analysis," whereas monadic `>>=` hides the continuation so you
> must run a step to see the next — the doc's "keep `Check` applicative → fold
> without executing" reasoning is **faithful** (it drops the "certain kinds of"
> hedge, minor). Its concession that "monadic cannot be analysed" is too absolute
> is **correct** (limited dummy-value introspection exists). "Literature supports
> but does not dictate the encoding" is a **reasonable** stance.

## Appendix C — raw findings: logic / inference foundations

> Monotonic (negation-free) Datalog has a unique least Herbrand model = least
> fixpoint of the immediate-consequence operator, reached by terminating bottom-up
> forward chaining over a finite Herbrand base — **Accurate**. Negation-as-failure
> is genuinely non-monotonic; **stratified negation** (every negated predicate
> fully computed in a strictly lower stratum) is the standard remedy giving a
> unique perfect model independent of the chosen stratification — **Accurate but
> understated**: it does *not* cover non-stratifiable (cyclic) negation, and
> **aggregation** (count/sum/min over derived facts) is a *separate*
> non-monotonic hazard the docs never mention. Kleene strong three-valued AND/OR
> are min/max over F<U<T, hence commutative & associative — **Accurate**.
> Per-fact provenance ≈ why-/how-provenance and justification-based TMS —
> **Accurate** (caveat: why-provenance for recursive Datalog can blow up; the
> "collect all reasons, sort" fix for order-sensitive messages is sound). Rete =
> classical efficient production-rule matching — **Accurate**. "Confluent by
> construction" is *conditional* on staying in the stratified, aggregation-safe,
> function-free fragment, and should be stated as a precondition.

## Appendix D — raw findings: current agent-governance landscape

> LLM-as-judge is the default for open-ended artifacts; non-determinism is handled
> *statistically* (N runs, mean±σ, both orderings), not by caching. Eval tools
> (Promptfoo, Inspect AI) cache on a digest of the **model request** (model id +
> prompt + config) for cost/latency — they cache the *generation*, not a frozen
> content-addressed *verdict artifact*. FS.GG's "freeze verdict, key by content
> hash of rule + artifacts, re-consult on change" is Bazel-style memoization of
> judgments — principled, but **not** a named eval pattern, and its key **omits
> the judge model/prompt version** (silent staleness on upgrade). Guardrails (NeMo,
> Guardrails AI) are *runtime* filters — orthogonal. Tiered machine/agent/human
> "who decides" maps onto established L1–L5 autonomy and 3-tier HITL approval
> gates. AI review as a required merge check + policy-as-code merge gates is the
> consolidating pattern; Spec Kit's `constitution.md` is prompt-context only, so
> FS.GG's recomputing merge boundary fills a real gap. Top risks: (1) judge-model
> drift invalidates the cache; (2) frozen single verdict = frozen noise; (3)
> prompt injection of the reviewer via governed artifacts; (4) gaming of advisory
> signals + normalized ungoverned sandbox; (5) cost/latency + judge-vs-human
> meta-validation debt.
