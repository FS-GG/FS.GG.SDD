---
title: Open questions
category: Governance design
categoryindex: 7
index: 11
description: Open questions and risks surfaced by the 2026-06-18 offline+online research review — operational LLM-judge realities and a few understated formal edge cases to resolve before/while implementing the kernel.
---

# Open questions

These are the gaps and risks surfaced by the
[2026-06-18 research review](../reports/2026-06-18-governance-design-research.md)
(offline synthesis + adversarial online verification). The design's *theory* held
up well under verification; these are the things to resolve before or during
implementation. Roughly in priority order. Each is tracked as a GitHub issue.

## 1. The agent-verdict cache key must include the judge

`AgentReviewed` verdicts are frozen and keyed by a content hash of the rule
([`Check.hash`](rule-edsl.md)) plus the artifacts it reads. That key **omits the
judge model and prompt version**. A silent model or prompt upgrade would reuse a
stale verdict on identical inputs. Industry eval caches (Promptfoo, Inspect AI)
key on the full model *request* — model id + prompt + config + inputs. **Decision
needed:** fold judge-model id + version + reviewer-prompt hash into the cache key,
and define the re-review policy when the judge changes.

## 2. A single frozen verdict bakes in judge noise

LLM judges are non-deterministic; the field mitigates statistically (run N times,
report mean±σ, test both orderings) rather than trusting one sample. Freezing one
verdict captures that run's position/scoring bias. **Decision needed:** whether
`AgentReviewed` should aggregate N runs or require a confidence threshold before a
verdict is frozen, and how that interacts with the evidence artifact.

## 3. Prompt injection of the agent reviewer

Governed artifacts (code, specs, design tokens, essays, research) are author- and
potentially attacker-controlled, and they are the *input* to the agent reviewer.
An injected instruction inside a governed artifact can flip an `AgentReviewed`
verdict. **Decision needed:** input isolation / instruction-data separation for
reviewer prompts, and whether reviewer outputs need their own integrity check.

## 4. "Confluent by construction" is conditional

The [confluence argument](theory-and-composition.md) is sound *inside* the
stratified-negation, function-free fragment — but it (a) does not cover
non-stratifiable (cyclic) negation, which has no stratified model, and (b) never
addresses **aggregation** (count/sum/min over derived facts), a separate
non-monotonic hazard. **Decision needed:** state these as explicit preconditions
of the kernel, and either forbid or stratify aggregation and recursive negation.

## 5. Gaming of advisory signals + meta-validation debt

Advisory-by-default is the right default, but it invites authors (human or agent)
to ignore or learn-to-pass advisory reviews, and `Sandbox` normalizes an
ungoverned inner loop. Separately, `AgentReviewed` rules need ongoing
judge-vs-human calibration or cached verdicts silently decay, and per-review
cost/latency can quietly undercut "light by default." **Decision needed:** a
lightweight judge-vs-human meta-evaluation loop and a cost/latency budget for
agent reviews.

## 6. Trim the policy-engine analogy

[Theory and composition](theory-and-composition.md) presents Cedar and OPA as
prior art "at industrial scale." Verification: Cedar validates the architecture
strongly (rules-as-data + multiple interpreters incl. SMT + deterministic
`forbid`-overrides combination); **OPA does not** carry the "multiple interpreters
folded deterministically" property, and **both are runtime allow/deny
authorization**, not build/merge-time artifact governance. **Decision needed:**
narrow the OPA claim and frame the analogy as architectural, not same-problem.

---

These do not block the design; the kernel, tiers, reified algebra, and routing are
validated. Items 1–3 are the ones that bite once `AgentReviewed` rules actually
run against a live model.
