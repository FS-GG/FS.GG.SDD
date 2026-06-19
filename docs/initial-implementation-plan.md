---
title: Implementation plan (Spec Kit)
category: Governance
categoryindex: 6
index: 12
description: Detailed, Spec-Kit-tailored implementation plan that decomposes the governance kernel, adapters, CLI, capability catalog, ship gate, native SDD flow, generated views, and release governance into standard Spec Kit features, each respecting the constitution's FSI-first / .fsi-visibility / Elmish-MVU / Tier-1-2 discipline and the light-by-default, light-dependency stance.
---

# Implementation plan (Spec Kit)

This is the **detailed** implementation plan for FS.GG.Governance. It elaborates
the org repository's coarse staged plan
([Stages G1–G5](https://github.com/FS-GG/.github/blob/main/docs/governance-implementation-plan.md))
and turns the [governance design](governance-design/index.md) into a concrete,
ordered set of **standard Spec Kit features** — each a `specs/NNN-slug/` unit run
through `specify → plan → tasks → implement` per the
[constitution](../.specify/memory/constitution.md).

It is a roadmap (the plan that *generates* the per-feature `plan.md`s), not a
substitute for them. Status as of 2026-06-18: F01-F12 are implemented and
verified on `main` (`dotnet build FS.GG.Governance.sln`, `dotnet test
FS.GG.Governance.sln`); F13 external validation remains. Status as of
2026-06-19: the
[governance capability design report](reports/2026-06-18-233718-fsgg-governance-capability-design.md)
has been incorporated as the F14-F27 continuation covering the capability
catalog, protected-boundary ship gate, native SDD bootstrap, normalized work
model, generated views, product/package/docs/skills/design checks, release
governance, and provenance hardening.

## 1. How this plan relates to the others

| Document | Role |
|---|---|
| [Governance design](governance-design/index.md) | *What* the system is (kernel, CheckTier, reified `Check`, routing, adapters). |
| [Org Stages G1–G5](https://github.com/FS-GG/.github/blob/main/docs/governance-implementation-plan.md) | *When* — coarse milestones and the adoption bar. |
| [Open questions](governance-design/open-questions.md) (issues [#1–#6](https://github.com/FS-GG/FS.GG.Governance/issues)) | Decisions to lock as we hit the relevant features. |
| [Capability design report](reports/2026-06-18-233718-fsgg-governance-capability-design.md) | Product-neutral capability envelope, `.fsgg` source artifacts, protected ship gate, native SDD flow, generated views, product checks, release/provenance bar. |
| **This plan** | *How*, in Spec Kit units — the feature sequence, surfaces, tests, and exit criteria. |

The mapping to org stages: the **kernel + evidence + JSON explanation**
(features F01–F06) *is* the "first useful product" (G2/G3); the **CLI + external
run** (F12–F13) is G3/G4; the **two-domain adapter set** (F09–F11) is the G5
adoption-bar evidence.

## 2. Ground rules every feature inherits (from the constitution)

Each feature's `spec.md`/`plan.md`/`tasks.md` MUST embody these — they are not
re-litigated per feature:

- **Order: Spec → FSI → Semantic Tests → Implementation.** Draft the public
  surface as a `.fsi`, exercise it in `scripts/prelude.fsx` / FSI first, write
  semantic tests against the *packed* library (or prelude), then implement `.fs`.
- **Visibility lives in `.fsi`.** Every public module ships a curated `.fsi`; no
  `private`/`internal`/`public` on top-level `.fs` bindings. A surface-area
  baseline per public module, checked by a drift test.
- **Change classification.** Each feature declares **Tier 1** (new/changed public
  API — full artifact chain incl. `.fsi` + baseline updates) or **Tier 2**
  (internal). Almost every feature below is Tier 1.
- **Elmish/MVU at the edge only.** The kernel and all interpreters are **pure** —
  Principle IV is *not applicable* to F01–F07, F09–F11. The effects shell (F08)
  and CLI (F12) are the stateful/IO features and MUST expose
  `Model`/`Msg`/`Effect`/`init`/`update` + an edge interpreter, with the only
  nondeterminism (agent calls) pushed to the boundary and reified as evidence.
- **Test evidence is mandatory; prefer real evidence.** Synthetic evidence is
  allowed only when disclosed (use-site `// SYNTHETIC:` comment, `Synthetic`
  token in the test name, PR listing). No evidence-audit gate machinery (it was
  stripped); disclosure is the discipline.
- **Idiomatic simplicity & observability.** Plain F#; complex features justified
  in the spec. Operationally significant events emit structured diagnostics;
  errors fail fast or degrade explicitly.
- **Light dependencies / dependency direction.** The kernel takes **no**
  dependency on FAKE, git, filesystem scanning, Skia, NuGet publishing, template
  profiles, or rendering paths. Generic code carries zero domain vocabulary.
  Governance may inspect a project; a project must never require governance.

## 3. Target architecture & solution layout

`net10.0`, package identity `FS.GG.Governance.*`, pack to
`~/.local/share/nuget-local/`. Dependency arrows point downward (lower depends on
nothing above it):

```text
FS.GG.Governance.Kernel        pure; BCL only (incl. System.Text.Json)
  ├─ facts, rules, fixed-point, provenance        (F01)
  ├─ verdicts + Kleene 3-valued logic             (F02, may fold into F03)
  ├─ reified Check algebra + 4 interpreters       (F03)
  ├─ CheckTier + Rule bridge + review cache key   (F04)
  ├─ evidence model + taint over a DAG            (F05)
  ├─ JSON explanation + evidence-freshness        (F06)
  └─ routing: Stakes / Severity / RunMode / Route (F07)

FS.GG.Governance.Adapters.Spi  pure; depends on Kernel   (F09)
FS.GG.Governance.Adapters.SpecKit   depends on Spi       (F10)
FS.GG.Governance.Adapters.DesignSystem  depends on Spi   (F11)

FS.GG.Governance.Host          effects shell (IO); depends on Kernel + Spi   (F08)
FS.GG.Governance.Cli           depends on Host + adapters                    (F12)

tests/         semantic tests load the PACKED libraries or the prelude
scripts/prelude.fsx   FSI entry point used by spec + tests
surface/       per-module surface-area baselines
fixtures/      non-rendering sample artifacts (a tiny RON/JSON tree, an essay)
```

Rationale for keeping the algebra *in* the kernel: per the design the four
interpreters and `CheckTier` carry zero domain vocabulary, so adapters reuse them
and supply only facts/artifacts/probes/rules/fences. JSON lives in the kernel
because `System.Text.Json` ships with the runtime (no new dependency).

## 4. The feature roadmap

Each feature is a Spec Kit unit. Entries give: **intent** (the spec's
user-visible outcome), **Tier**, **public surface** (the `.fsi` to design first),
**FSI focus**, **semantic-test focus**, **MVU**, **depends on**, **exit
criteria**. Run them roughly in order; `[P]` marks ones that can proceed in
parallel once their dependencies land.

Progress markers are deliberately checkbox-shaped so this document can double as
the working board:

- [x] Complete on `main`.
- [ ] Planned or not yet started.
- [ ] A feature becomes complete only after its spec, plan, tasks, implementation,
  tests, surface baseline, readiness evidence, and documentation deltas land.

### Phase A — The kernel (the first useful product) · org G2–G3

#### F01 · `001-kernel-core` — ✅ Done (merged to `main`)
- **Intent:** a pure reasoner derives facts from asserted facts + rules to a fixed
  point and records why each derived fact holds.
- **Tier:** 1.
- **Surface:** `FactId`, `RuleId`, `ProvenanceStep`, `FactAssertion<'fact>`,
  `FactSet<'fact>`, `Rule<'fact>`, `EvaluationResult<'fact>`,
  `FixedPoint.evaluate (identify) (rules) (supplied)`.
- **FSI focus:** assert a handful of toy facts + 2–3 monotonic rules; watch
  derivation reach quiescence; inspect provenance.
- **Tests:** termination on a bounded fact space; **order-independence** (shuffle
  rule order → identical least fixed point); provenance records rule + inputs;
  injective `identify` dedup.
- **MVU:** N/A (pure).
- **Depends on:** —.
- **Exit:** monotonic forward-chaining engine with provenance, zero deps, surface
  baseline recorded. **Locks decision #4** (kernel constraints): rules are
  monotonic; negated/aggregated facts are *supplied* (lower stratum), never
  derived in the same fixed point — documented as a precondition.

#### F02 · `002-verdicts-kleene` [P after F01] — ✅ Done (merged to `main`)
- **Intent:** three-valued verdicts compose order-independently.
- **Tier:** 1. **Surface:** `Verdict = Pass | Fail of string | Uncertain of string`
  + Kleene combinators `Verdict.all`/`any`/`negate` (list reductions, not binary
  operators — see `specs/002-verdicts-kleene/research.md` D2).
- **Tests:** commutativity/associativity of `all`/`any` combination; byte-for-byte
  order-/nesting-/duplication-independent reason aggregation; `Uncertain`
  is not silently coerced to pass/fail. **MVU:** N/A. **Depends on:** F01.
- **Exit:** verdict algebra ready for the `Check` interpreters — `Verdict.fsi`/`.fs`
  added to the kernel, surface baseline re-blessed, 11 new tests (V1–V10b) green,
  zero new dependencies. *(Kept separate from F03, not folded in.)*

#### F03 · `003-check-algebra` — ✅ Done (merged to `main`)
- **Intent:** a rule's check is one reified value that can be evaluated, rendered,
  hashed, and explained from a single source.
- **Tier:** 1.
- **Surface:** `ArtifactRef`, `Outcome`, `ProbeArg`, `Probe<'fact>`,
  `Check<'fact> = Atom | All | Any | Not | Implies | Opaque`; smart constructors
  (`probe`, `allOf`, `anyOf`, `not'`, `==>`, `.&`, `.|`); `Check.eval/render/hash/
  explain/reads/isReified`.
- **FSI focus:** build a small check by hand; fold it six ways; confirm
  `render`/`hash` work **without executing** `Eval`.
- **Tests:** `eval` Kleene semantics; `hash` canonicalizes commutative nodes
  (`All [a;b] == All [b;a]`) but stays positional for `Implies`/`Atom` args
  (**decision #4 / hazard 3**); `explain` proof tree matches `eval`; `isReified`
  is false iff an `Opaque` is present.
- **MVU:** N/A (applicative, no `bind`). **Depends on:** F02.
- **Exit:** the inspectable algebra + six interpreters; the keystone of the design.
  Done — `Check.fsi`/`.fs` added to the kernel, surface baseline re-blessed, 14 new
  tests (V1–V12) green, zero new dependencies (SHA-256 is `System.*`). **Locks
  decision #4 / hazard 3:** commutative-node hash canonicalization (ordinal-sort child
  digests for `All`/`Any`; positional for `Implies` and probe `Args`/`Reads`).

#### F04 · `004-checktier-rule-bridge` — ✅ Done (merged to `main`)
- **Intent:** every rule declares who is competent to decide it, and agent
  reviews are cached so a stochastic judge stays reproducible.
- **Tier:** 1.
- **Surface:** `CheckTier = Deterministic | AgentReviewed | HumanOnly`;
  `Severity = Advisory | Blocking`; `SpecSource`; `Rule` record (tier/spec/
  severity/check/question); `rule`, `blocking`, `asking`; `toRule`;
  review-request + recorded-verdict facts; the cache-key function.
- **Tests:** `rule` **refuses `Deterministic` when `not isReified`** (forces
  Agent/Human); cache hit vs miss; key changes when inputs change.
- **MVU:** N/A (pure; the actual agent call is F08). **Depends on:** F03.
- **Exit:** the bridge from `Check` to kernel `Rule`. **Locks decision #1**: the
  cache key = `Check.hash` + artifact hashes **+ judge model id + judge version +
  reviewer-prompt hash**, with a defined re-review policy when the judge changes.
  **Notes decision #2** (single-sample noise — whether to aggregate N runs /
  require a confidence threshold before freezing) for the F08 interpreter.

#### F05 · `005-evidence-model` [P after F01] — ✅ Done (merged to `main`)
- **Intent:** evidence state is tracked and synthetic taint propagates over the
  dependency graph and clears when the root cause is upgraded.
- **Tier:** 1.
- **Surface:** `EvidenceState = Pending | Real | Synthetic | Failed | Skipped |
  AutoSynthetic`; `EvidenceGraph` (DAG, rejects cycles); `effective` taint
  closure.
- **Tests:** transitive `AutoSynthetic` flow; auto-clear on `Synthetic → Real`;
  cycle rejection; least-fixed-point determinism. Generalize beyond software (a
  finding on simulated data is `AutoSynthetic`).
- **MVU:** N/A. **Depends on:** F01. **Exit:** evidence taint as a kernel
  derivation, not a bespoke engine. **Reinforces #4** (DAG only; no cycles).

#### F06 · `006-explanation-output` — ✅ Done (merged to `main`)
- **Intent:** explanations, the rendered rule contract, and evidence-freshness
  predicates are emitted as JSON-friendly, human/agent-readable output.
- **Tier:** 1.
- **Surface:** `Explanation` serialization; `contract : Rule list -> ...`
  (a *fold of the rules*, not a hand-maintained file); evidence-freshness
  predicates (the "simple freshness" from the first-product scope).
- **Tests:** contract is the rendered selector (cannot drift); JSON round-trips;
  freshness predicates over fixture timestamps.
- **MVU:** N/A. **Depends on:** F03, F05.
- **Exit:** **the first useful product is complete** — a kernel that stores facts,
  evaluates rules to a fixed point, carries provenance, taints synthetic
  evidence, and emits JSON explanations, with zero heavy deps. Pack
  `FS.GG.Governance.Kernel` to `~/.local/share/nuget-local/`.

### Phase B — Routing & the effects edge · org G3

#### F07 · `007-routing-severity-modes` — ✅ Done (merged to `main`)
- **Intent:** a change gets only the proof its risk warrants, and every routing
  decision explains itself.
- **Tier:** 1.
- **Surface:** `ChangeSet` (abstract), `Stakes = Routine | Fenced of string`,
  `Fence`, `stakesOf`; `RunMode = Sandbox | Inner | Gate`; `Route` +
  `renderRoute`.
- **FSI focus:** a no-fence change → "light — no gates"; a fenced change → a
  blocking gate that names rule + fence + rendered check.
- **Tests:** light-by-default (unclassified ⇒ `Routine`, no gates); blocking set
  is filterable and short; `Route` always carries a reason; combination is
  deterministic-precedence (forbid-trumps-permit), **never positional** (decision
  #4 / hazard 5).
- **MVU:** N/A (pure). **Depends on:** F04.
- **Exit:** light, advisory, explainable routing over the kernel.

#### F08 · `008-effects-interpreter` — ✅ Done (`FS.GG.Governance.Host`; 18/18 tests; completes M2)
- **Intent:** the impure shell gathers facts, runs the pure kernel, and interprets
  effects (read artifacts, dispatch an agent review, record a verdict) at the
  edge.
- **Tier:** 1.
- **Surface (MVU):** `Model` (loaded facts + pending reviews), `Msg`
  (artifact-read results, agent verdicts, transitions), `Effect`/`Cmd<Msg>`
  (ReadArtifact / DispatchReview / RecordVerdict), `init`, pure `update`, and an
  interpreter that executes effects.
- **FSI focus:** drive `init`/`update` through the packed library; assert emitted
  effects without running IO.
- **Tests:** pure transition tests (Model+Msg ⇒ Model+effects); interpreter tests
  against a **real** filesystem fixture; a recorded agent verdict round-trips and
  hits the F04 cache on re-run.
- **MVU:** **applicable** — this is the boundary feature.
- **Depends on:** F06, F07.
- **Exit:** sense→plan→act loop with nondeterminism reified as evidence. **Locks
  decision #2** (aggregate/threshold before freezing a verdict) and **decision
  #3** (reviewer prompt-injection: treat governed artifacts as untrusted data,
  isolate instruction vs. data in the review prompt). **Opens decision #5**
  (cost/latency budget + judge-vs-human meta-validation) as a tracked deferral.

### Phase C — Adapters & composition (the adoption bar) · org G5

#### F09 · `009-adapter-spi` — ✅ Done (merged to `main`)
- **Intent:** a domain plugs in by supplying only facts, an artifact mapping,
  probes, a rule catalog, and fences; everything else is reused.
- **Tier:** 1.
- **Surface:** the adapter interface; the composition root — a coproduct
  `ProjectFact` with `Rule.contramapFacts` lifting and single-case active
  patterns; deterministic, order-independent cross-domain precedence.
- **Tests:** an adapter's rules lift into `ProjectFact` and evaluate unchanged;
  a cross-domain `Implies` rule is order-independent; removing one adapter leaves
  the kernel + other adapters intact (the boundary test).
- **MVU:** N/A. **Depends on:** F04 (+F05). **Exit:** the SPI + composition root;
  the "kernel is a library, not a platform" contract made concrete.

#### F10 · `010-adapter-speckit` — ✅ Done (merged to `main`)
- **Intent:** the Spec Kit workflow is governed as data — phases and task states
  are facts, phase checks are reified rules, the merge boundary is the one fence.
- **Tier:** 1.
- **Surface:** `SpecKitArtifact`, `Phase`, `SpecKitFact`
  (`PhaseReached`/`ArtifactPresent`/`TaskState`/`TaskDependsOn`/…), `whenPhase`
  guard, the phase-check rule catalog, `mergeFence`, constitution-as-dial.
- **Tests:** `whenPhase Plan` contributes only at/after Plan; everything is
  advisory in the inner loop and only `merge` flips to `Gate`; the
  evidence/`TaskDependsOn` graph runs through the F05 model (not a bespoke
  engine).
- **MVU:** N/A. **Depends on:** F09. **Exit:** governance dogfoods **this repo's
  own** Spec Kit workflow — domain #1 for the adoption bar.

#### F11 · `011-adapter-designsystem` — ✅ Done (merged to `main`; completes M3)
- **Intent:** a second, unrelated domain governs a design language from fixtures,
  proving generality without copying domain #1's shape.
- **Tier:** 1.
- **Surface:** `DesignArtifactRef`, design probes (`surfaceMatches`,
  `contrastMeets`), the tiered rule catalog (deterministic token/contrast =
  blocking; judgement rules via `Opaque` ⇒ AgentReviewed).
- **Tests:** deterministic rules render + hash; `Opaque` judgement rules route to
  an agent with the rule's `Question`; runs against a **fixture** token tree (no
  rendering dependency, no rendering vocabulary in generic code).
- **MVU:** N/A. **Depends on:** F09. **Exit:** domain #2 — **adoption bar met**
  (two unrelated domains adopt the kernel cheaply). Done: the new
  `FS.GG.Governance.Adapters.DesignSystem` library supplies only its SPI
  components, references Spi but not F10, runs the full catalog over fixture
  facts, proves the tier split and stable render/hash behavior, and lifts
  unchanged beside the real Spec Kit adapter.

### Phase D — CLI & external validation · org G3–G4

#### F12 · `012-cli` — ✅ Done (merged to `main`)
- **Intent:** a person or CI runs `route` / `explain` / `contract` / evidence
  report against a repo snapshot and gets text or JSON out.
- **Tier:** 1.
- **Surface (MVU/edge):** CLI commands wired to the F08 interpreter; text +
  `--json`; exit codes (advisory = 0, blocking-fail at `Gate` = nonzero).
- **Tests:** smoke runs against the fixtures and against this repo's own
  `.specify` tree (dogfood); JSON output is stable; `Sandbox`/`Inner`/`Gate`
  behave per mode.
- **MVU:** applicable (IO at the edge). **Depends on:** F08, F10 (+F11). **Exit:**
  the optional CLI tool (org G3); pack as a tool to `~/.local/share/nuget-local/`.
  Done: `FS.GG.Governance.Cli` is a packable `fsgg-governance` tool with
  `route`, `explain`, `contract`, and `evidence`; public `Project.fsi`/`Cli.fsi`
  signatures, CLI MVU transition tests, fixture/repository command smokes, stable
  JSON evidence, exit-code evidence, package-install smoke, read-only evidence, and
  a CLI surface baseline are recorded under `specs/012-cli/readiness/`.

#### F13 · `013-run-against-external-repo`
- **Intent:** point the tool at an external checkout (a rendering repo, or a
  Sojourn fixture) from the outside and produce an advisory report.
- **Tier:** 1 (mostly docs + an adapter/fixture; little new kernel surface).
- **Tests:** the external repo needs **no** dependency on governance; removing the
  tool changes nothing for it; findings convert to ordinary issues/tasks.
- **MVU:** reuses F08/F12. **Depends on:** F12. **Exit:** org **G4** — validated
  against an external customer from outside; advisory by default (org **G5**
  adoption decision starts here).

### Phase E — Capability catalog and protected-boundary skeleton · capability design phase 1

#### F14 · `014-fsgg-project-policy-capability-schemas` — [ ] Planned
- **Intent:** a governed product can declare identity, policy, capability scope,
  tool policy, and protected surfaces in versioned `.fsgg` source files before
  any product-specific adapter exists.
- **Tier:** 1.
- **Surface:** `ProjectConfig`, `PolicyConfig`, `CapabilityCatalog`,
  `ToolingConfig`, `SchemaVersion`, `GovernedRoot`, `ProtectedSurface`,
  `CapabilityId`, `ProfileId`; strict parse/validate/render functions in Host or
  a new light configuration library, with Kernel receiving only typed facts.
- **Implementation checklist:**
  - [ ] Define MVP schemas for `.fsgg/project.yml`, `.fsgg/policy.yml`,
    `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml`.
  - [ ] Support schema versioning, deterministic ordering, duplicate-id errors,
    unknown-field diagnostics, and path normalization.
  - [ ] Model `domains`, `pathMap`, `surfaces`, `checks`, `defaultProfile`,
    command allow-list, environment classes, and timeouts.
  - [ ] Add fixture products covering routine docs, governed roots, protected
    package/API surfaces, generated views, and release surfaces.
  - [ ] Emit typed facts without leaking YAML or product vocabulary into Kernel.
- **Tests:** strict YAML validation, deterministic re-rendering where supported,
  path normalization on Unix/Windows-shaped inputs, unknown-field failures,
  duplicate id failures, and fixtures for all MVP files.
- **MVU:** N/A except CLI/Host effect boundary reads. **Depends on:** F12.
- **Exit:** a minimal `.fsgg` catalog answers what changed, why a gate would run,
  and what governed path is unknown.

#### F15 · `015-git-ci-snapshot-facts` — [ ] Planned
- **Intent:** route and ship commands sense the repository boundary from typed
  git/CI facts instead of caller-provided prose.
- **Tier:** 1.
- **Surface:** `GitRef`, `DiffRange`, `ChangedPath`, `RepoSnapshot`,
  `CiContext`, `WorkingTreeState`, `SnapshotOptions`; Host process-runner facade
  functions for read-only `git` calls.
- **Implementation checklist:**
  - [ ] Sense base ref, head ref, merge base, changed paths, dirty paths,
    untracked paths, branch name, and optional PR/status metadata.
  - [ ] Keep git access behind the existing Host MVU/effect boundary.
  - [ ] Normalize changed paths relative to the governed root.
  - [ ] Add `--since`, `--base`, `--head`, and `--paths` resolution contracts for
    future route/ship commands.
  - [ ] Record command-run digests for sensed git operations without embedding
    nondeterministic output in stable JSON.
- **Tests:** git fixture repository, dirty/untracked fixtures, base/head parity,
  stable path ordering, failed git command diagnostics, and read-only behavior.
- **MVU:** applicable in Host. **Depends on:** F14.
- **Exit:** all route and ship inputs can be reproduced from typed snapshots.

#### F16 · `016-capability-routing-gate-registry` — [ ] Planned
- **Intent:** changed paths map to capabilities, protected surfaces, and stable
  gate identities with light-by-default behavior preserved.
- **Tier:** 1.
- **Surface:** `GateId`, `GateMetadata`, `GateRegistry`, `RouteTrace`,
  `UnknownGovernedPath`, `CapabilityMatch`, `GlobPrecedence`, `GateCost`,
  `GateMaturity`.
- **Implementation checklist:**
  - [ ] Implement deterministic glob precedence for `pathMap` and surface paths.
  - [ ] Emit `unknownGovernedPath` only under declared governed roots or protected
    boundaries, never as a global default-deny fallthrough.
  - [ ] Generate or assemble `.fsgg/gates.json` with id, domain, prerequisites,
    cost, timeout, owner, maturity, product-check flag, and freshness key.
  - [ ] Extend `route` output to explain changed path, matched capability,
    selected gate, rule, cost, owner, cache eligibility, and cheaper local
    alternative when known.
  - [ ] Keep routine unmanaged notes/reports/drafts cheap unless a declared fence
    matches.
- **Tests:** governed versus unmanaged unknown paths, protected-surface
  classification, glob precedence, deterministic gate ordering, route trace
  snapshots, and no heavy-route fallback for unrelated files.
- **MVU:** pure plus CLI projection. **Depends on:** F14, F15.
- **Exit:** the routing safety policy from the capability design is executable.

#### F17 · `017-ship-walking-skeleton` — [ ] Planned
- **Intent:** `fsgg ship --mode gate --profile standard --json` becomes the first
  protected-branch contract before the full lifecycle command suite exists.
- **Tier:** 1.
- **Surface:** `ShipOptions`, `ShipReport`, `AuditFinding`, `ExitCodeBasis`,
  `ProfileAdjustedFinding`, JSON schema for `readiness/<id>/route.json` and
  `readiness/<id>/audit.json`.
- **Implementation checklist:**
  - [ ] Add `ship` command with `--mode gate`, `--profile`, `--json`, `--base`,
    `--head`, and governed-root options.
  - [ ] Recompute from base/head rather than trusting precomputed local route
    output.
  - [ ] Emit deterministic route and audit JSON with selected gates, unmatched
    governed paths, expected artifacts, cost, cache eligibility, profile-adjusted
    enforcement, and exit-code basis.
  - [ ] Make nonzero exit depend only on blocking effective findings.
  - [ ] Publish initial GitHub Actions branch-protection guidance.
- **Tests:** stable JSON snapshots, exit-code matrix, base/head recomputation,
  profile selection, route/audit consistency, and CLI smoke against this repo.
- **MVU:** applicable at CLI edge. **Depends on:** F16.
- **Exit:** the protected boundary has a runnable deterministic skeleton.

### Phase F — Policy dials, native SDD, and normalized work · capability design phases 2-3

#### F18 · `018-profiles-maturity-enforcement-fixtures` — [ ] Planned
- **Intent:** mode, profile, maturity, severity, and rule tier adjust enforcement
  without changing truth or hiding findings.
- **Tier:** 1.
- **Surface:** `GovernanceProfile`, `RunMode`, `RuleMaturity`,
  `BaseSeverity`, `EffectiveSeverity`, `EnforcementReason`,
  `EnforcementDecision`.
- **Implementation checklist:**
  - [ ] Parse `light`, `standard`, `strict`, and `release` profiles from
    `.fsgg/policy.yml`.
  - [ ] Preserve base verdict, base severity, rule hash, and finding visibility in
    all modes and profiles.
  - [ ] Implement effective severity rules for unknown paths, stale evidence,
    synthetic evidence, uncertain verdicts, generated-view drift, provenance, and
    release pack evidence.
  - [ ] Keep agent-reviewed findings advisory until cache identity,
    prompt-isolation, confidence, and calibration constraints are implemented.
  - [ ] Generate golden truth-table fixtures for every enforcement-affecting
    combination.
- **Tests:** full truth-table fixture set for run modes, profiles, maturity,
  base/effective severity, deterministic/agent/human tiers, fenced/routine
  routing, and unknown governed paths.
- **MVU:** pure. **Depends on:** F17.
- **Exit:** policy dials are explainable, reproducible, and snapshot-tested.

#### F19 · `019-native-sdd-bootstrap` — [ ] Planned
- **Intent:** a consumer can start a governed product and continue the
  spec-driven development loop through native FS.GG commands and artifacts.
- **Tier:** 1.
- **Surface:** `ProjectInitOptions`, `TemplateProviderRef`, `WorkId`,
  `SddStage`, `WorkArtifact`, command contracts for `new`, `init`, `charter`,
  `work specify`, `work clarify`, `work checklist`, `work plan`, `work tasks`,
  and `analyze`.
- **Implementation checklist:**
  - [ ] Add `fsgg new <path>` and `fsgg init` skeleton output for `.fsgg`,
    `work/`, readiness directory, and optional template-provider handoff.
  - [ ] Add command contracts for charter, specify, clarify, checklist, plan,
    tasks, and analyze, with advisory posture by default.
  - [ ] Generate agent command/skill stubs that drive the same native stages.
  - [ ] Keep project templates provider-neutral and governance-owned artifacts
    separate from runtime code.
  - [ ] Document the greenfield bootstrap path from the capability design.
- **Tests:** bootstrap fixture, idempotent init, template-provider absence,
  generated artifact layout, command help snapshots, and no governance dependency
  injected into generated runtime packages.
- **MVU:** applicable at CLI edge. **Depends on:** F14, F17.
- **Exit:** a new product has enough `.fsgg` and `work/<id>` structure for an
  agent or human to continue the SDD loop.

#### F20 · `020-normalized-work-model` — [ ] Planned
- **Intent:** gates evaluate a normalized `WorkModel`, not loose Markdown, while
  preserving Markdown as the human authoring surface.
- **Tier:** 1.
- **Surface:** `WorkModel`, `RequirementId`, `DecisionId`, `TaskId`,
  `EvidenceRequirement`, `WorkModelDiagnostic`, `WorkModelDigest`,
  `readiness/<id>/work-model.json` schema.
- **Implementation checklist:**
  - [ ] Parse `work/<id>/spec.md`, `clarifications.md`, `checklist.md`,
    `plan.md`, `contracts/`, `tasks.yml`, and `evidence.yml`.
  - [ ] Define conflict rules for missing typed requirements, unknown references,
    structured/prose disagreement, and stale generated `work-model.json`.
  - [ ] Prefer structured graph data for execution while surfacing prose
    conflicts as findings.
  - [ ] Emit source digests, model version, parse diagnostics, and conflict
    diagnostics.
  - [ ] Connect work model findings to profile/maturity enforcement.
- **Tests:** malformed Markdown, unknown requirement references, task dependency
  cycles, stale generated work model, deterministic JSON, and conflict severity
  by mode/profile.
- **MVU:** pure parsing plus CLI edge. **Depends on:** F18, F19.
- **Exit:** ship/verify can gate on typed work facts instead of Markdown drift.

### Phase G — Readiness, generated views, product surfaces, and cache · capability design phases 4-7

#### F21 · `021-readiness-report-suite` — [ ] Planned
- **Intent:** route, contract, explanation, evidence, audit, and human summary
  reports are generated from the same immutable report objects.
- **Tier:** 1.
- **Surface:** JSON schemas for `route.json`, `contract.json`, `explain.json`,
  `evidence.json`, `audit.json`, and `summary.md`; report assembly APIs that
  reuse Kernel/SPI data.
- **Implementation checklist:**
  - [ ] Produce `contract.json`, `explain.json`, `evidence.json`, `audit.json`,
    and `summary.md` under `readiness/<id>/`.
  - [ ] Include proof trees, rule contracts, source reads, effective evidence,
    taint propagation, freshness failures, blockers, warnings, and provenance
    references.
  - [ ] Ensure Markdown summary is a view over JSON, not an independent source of
    truth.
  - [ ] Keep nondeterministic timestamps out of deterministic JSON unless marked
    as metadata.
  - [ ] Preserve plain text and JSON command parity.
- **Tests:** JSON schema snapshots, deterministic ordering, summary derived from
  JSON, freshness/taint cases, and stale-readiness detection.
- **MVU:** CLI edge only. **Depends on:** F20.
- **Exit:** readiness artifacts can explain every selected gate and blocker.

#### F22 · `022-generated-view-refresh` — [ ] Planned
- **Intent:** `fsgg refresh` is the single regeneration entry point for generated
  views, baselines, catalogs, and readiness projections.
- **Tier:** 1.
- **Surface:** `GenerationManifest`, `GeneratedView`, `SourceDigest`,
  `RendererId`, `RefreshPlan`, `RefreshReport`, `GeneratedViewDrift`.
- **Implementation checklist:**
  - [ ] Define source/view/generator/currency relationships in a manifest.
  - [ ] Implement `fsgg refresh` for gate metadata, rule catalogs, capability
    docs, skill references, API-surface docs, work-model projections, and
    baselines.
  - [ ] Detect stale generated views and route drift findings through profiles.
  - [ ] Keep generated views clearly marked as outputs.
  - [ ] Add refresh dry-run and JSON output.
- **Tests:** source digest changes, renderer version changes, stale view blocking
  at gate, dry-run output, idempotent refresh, and generated-output snapshots.
- **MVU:** CLI edge. **Depends on:** F21.
- **Exit:** generated views cannot pass protected boundaries merely by existing.

#### F23 · `023-generated-product-capabilities` — [ ] Planned
- **Intent:** `.fsgg/capabilities.yml` expands from MVP routing into generated
  product, package, docs, skills, samples, design, release, and evidence tags.
- **Tier:** 1.
- **Surface:** extended capability schema for package surfaces, generated roots,
  template profiles, sample apps, docs/examples, skills, design artifacts,
  release surfaces, baselines, and evidence tags.
- **Implementation checklist:**
  - [ ] Model generated product roots, template profiles, package pins, product
    tests, generated guidance, sample apps, and release surfaces.
  - [ ] Add cost-tiered generated-product gates: structural scan, restore/build,
    focused tests, full verify, and release validation.
  - [ ] Support generated products running governance locally without monorepo
    access.
  - [ ] Replace any durable shell-owned product behavior with compiled commands.
  - [ ] Add migration notes for catalog schema evolution.
- **Tests:** generated-product fixture, missing template profile, package-pin
  drift, local-only governance run, cost-tier route selection, and schema
  migration snapshots.
- **MVU:** CLI/Host edge. **Depends on:** F16, F22.
- **Exit:** generated products declare enough capability scope to protect their
  own package, docs, skills, design, and release surfaces.

#### F24 · `024-package-docs-skills-design-checks` — [ ] Planned
- **Intent:** major capability domains have concrete deterministic checks before
  agent-reviewed judgement checks can influence gates.
- **Tier:** 1.
- **Surface:** adapter rule packs for package/API, docs/examples, skills, and
  design/rendering; `.fsi` baseline facts; FSI transcript facts; docs link facts;
  skill path-contract facts; token/capture/contrast/control facts.
- **Implementation checklist:**
  - [ ] Add `.fsi` surface baseline generation and drift checks.
  - [ ] Add FSI transcript checks for public examples and package contracts.
  - [ ] Add FsDocs/literate script/public API docs/link/reference currency checks.
  - [ ] Add skill-quality checks for product skills, task skill lists, path
    contracts, and optional mirrors.
  - [ ] Connect design-system facts to real token, capture, contrast, and control
    catalog sources while keeping rendering dependencies out of Kernel.
  - [ ] Keep judgement-heavy agent-reviewed checks advisory.
- **Tests:** package baseline drift, transcript fixture, docs link fixture, skill
  contract fixture, token/capture fixture, adapter composition, and advisory
  agent-reviewed findings.
- **MVU:** pure adapters plus Host sensors. **Depends on:** F23.
- **Exit:** common generated-product surfaces have executable deterministic
  governance coverage.

#### F25 · `025-cost-cache-command-provenance` — [ ] Planned
- **Intent:** expensive evidence is reused only when its freshness key proves it
  applies to the current rule, artifact, command, environment, and base/head.
- **Tier:** 1.
- **Surface:** `FreshnessKey`, `EvidenceCacheEntry`, `CommandRun`,
  `EnvironmentClass`, `ArtifactDigest`, `RuleHash`, `GeneratorVersion`,
  `CacheDecision`, `CostBudget`.
- **Implementation checklist:**
  - [ ] Cache evidence by rule hash, artifact hashes, command version, generator
    version, base/head, environment class, and output digest.
  - [ ] Record command runs for builds, tests, packs, template instantiation, git
    diffs, package inspection, and visual captures.
  - [ ] Enforce max-cost per profile and mode, with clear reasons for skipped or
    deferred expensive gates.
  - [ ] Add stale evidence, synthetic taint, and cache-invalidated findings.
  - [ ] Include agent review cache identity fields without promoting those checks
    to blockers.
- **Tests:** cache hit/miss matrix, rule hash invalidation, artifact digest
  invalidation, environment-class mismatch, command version mismatch, cost-budget
  enforcement, and audit provenance snapshots.
- **MVU:** Host edge. **Depends on:** F18, F21, F24.
- **Exit:** cost control is part of correctness, not a CLI convenience.

### Phase H — Verify, release, and human projections · capability design phase 8

#### F26 · `026-verify-release-provenance` — [ ] Planned
- **Intent:** `fsgg verify` and `fsgg release` validate publication boundaries
  with package, version, publish-plan, template-pin, and provenance evidence.
- **Tier:** 1.
- **Surface:** `VerifyReport`, `ReleaseReport`, `PackageEvidence`,
  `VersionPolicy`, `PublishPlan`, `AttestationSummary`, `ReleaseExitCodeBasis`.
- **Implementation checklist:**
  - [ ] Define `verify` and `release` schemas, JSON output, and exit codes.
  - [ ] Add rules for version bumps, package metadata, pack outputs, trusted
    publishing posture, publish plans, template pins, and release provenance.
  - [ ] Pack every packable project with bumped version number before release
    gates can pass.
  - [ ] Add scheduled exhaustive validation hooks for broad matrices.
  - [ ] Emit SLSA/in-toto-shaped metadata without overclaiming formal compliance.
- **Tests:** version-bump matrix, pack evidence fixture, publish-plan fixture,
  template-pin drift, release profile enforcement, and attestation summary
  snapshots.
- **MVU:** CLI/Host edge. **Depends on:** F25.
- **Exit:** publication has a blocking governance boundary distinct from ship.

#### F27 · `027-human-projections-watch-tui` — [ ] Planned
- **Intent:** humans get useful plain text, Spectre.Console, watch, and optional
  TUI views without diverging from JSON automation truth.
- **Tier:** 2 unless public CLI/API surface changes require Tier 1.
- **Surface:** presentation models over `RouteReport`, `VerifyReport`,
  `ShipReport`, `ReleaseReport`, and readiness artifacts; optional `watch` and
  `tui` command contracts.
- **Implementation checklist:**
  - [ ] Render route, evidence, verify, ship, and release reports from immutable
    report objects.
  - [ ] Add Spectre.Console projections in CLI only.
  - [ ] Add `watch` projection over route/evidence/check reports.
  - [ ] Preserve plain text as human-readable but non-contractual output.
  - [ ] Keep JSON deterministic and presentation-free.
- **Tests:** ANSI-free JSON, stable plain-text smoke snapshots, terminal-width
  resilience, watch debounce fixture, and report-object parity.
- **MVU:** CLI edge. **Depends on:** F21, F26.
- **Exit:** operator UX improves without creating a second source of truth.

## 5. Cross-cutting concerns

- **Surface baselines + drift test** (Principle II): stand up the API-surface
  baseline mechanism alongside F01 and extend it per public module thereafter.
- **Observability:** pick a structured-logging approach — `TODO(STRUCTURED_LOGGING)`
  in the constitution. Record the choice in an ADR (`docs/decisions/`) before F08
  (the first feature that does real IO).
- **Packaging:** `Directory.Build.props` / `Directory.Packages.props`, `net10.0`,
  `FS.GG.Governance.*` ids, pack to `~/.local/share/nuget-local/`. The Kernel
  packs after F06; the CLI tool after F12.
- **FSI prelude:** `scripts/prelude.fsx` loads the packed kernel for spec-time
  sketching and semantic tests (the constitution's "exercise through the same FSI
  surface" rule).
- **Fixtures:** a tiny, non-rendering sample tree (a few JSON/RON files + an essay
  doc) so adapters and the CLI can be tested without any consumer repo.
- **Schema versioning:** every `.fsgg` and `work/<id>` machine-owned schema from
  F14 onward carries a version, strict validation, stable diagnostics, and
  migration notes before it can become a gate input.
- **Protected-boundary-first delivery:** F14-F18 are prioritized over the full SDD
  command suite because `ship --mode gate --profile standard --json` proves the
  protected-boundary value early.
- **Deterministic JSON:** route, work-model, refresh, evidence, ship, verify, and
  release JSON are the automation contracts; Markdown and terminal views are
  generated projections.
- **Light-by-default invariant:** new capability routing must never reintroduce a
  global default-deny fallback. Unknown paths block only when they are under
  declared governed roots or protected boundaries and the active policy says so.
- **Agent-review discipline:** agent-reviewed rules remain advisory until cache
  identity, prompt isolation, confidence thresholds, and judge-vs-human
  calibration evidence are implemented and promoted deliberately.

## 6. Decisions to lock (from the open questions)

| # | Decision | Locked at |
|---|---|---|
| [#1](https://github.com/FS-GG/FS.GG.Governance/issues/1) | Agent-review cache key includes judge model id + version + prompt hash; define re-review-on-judge-change policy. | F04 |
| [#2](https://github.com/FS-GG/FS.GG.Governance/issues/2) | Aggregate N runs / require a confidence threshold before freezing a verdict. | F04 spec → F08 impl |
| [#3](https://github.com/FS-GG/FS.GG.Governance/issues/3) | Reviewer prompt-injection: governed artifacts are untrusted data; isolate instruction vs. data. | F08 |
| [#4](https://github.com/FS-GG/FS.GG.Governance/issues/4) | Kernel preconditions: monotonic; stratify negated facts; forbid/stratify aggregation & recursive negation; commutative-node hash canonicalization. | F01, F03, F05 |
| [#5](https://github.com/FS-GG/FS.GG.Governance/issues/5) | Cost/latency budget for agent reviews + a judge-vs-human meta-validation loop. | Fresh-review budget locked at F12; judge-vs-human meta-validation remains F13+ |
| [#6](https://github.com/FS-GG/FS.GG.Governance/issues/6) | Narrow the OPA claim; frame the policy-engine analogy as architectural. | Docs task (no code) |

Additional decisions introduced by the capability design report:

| # | Decision | Locked at |
|---|---|---|
| C1 | Minimal `.fsgg/capabilities.yml` shape, schema versioning, and glob precedence. | F14-F16 |
| C2 | Unknown-path policy: only unknown governed paths or protected-boundary paths can block. | F16-F18 |
| C3 | `ship --mode gate --profile standard --json` is the first protected-branch contract. | F17 |
| C4 | Profiles alter effective enforcement only; they never change truth, rule hashes, or finding visibility. | F18 |
| C5 | Gates evaluate normalized `WorkModel` artifacts, not Markdown prose directly. | F20 |
| C6 | Generated views are outputs with currency gates, not trusted source artifacts. | F22 |
| C7 | Release provenance emits compatible metadata first and claims formal compliance only after explicit verification. | F26 |

## 7. Milestones

Progress board:

- [x] M1 — First useful product (F01-F06).
- [x] M2 — Light routing + effects edge (F07-F08).
- [x] M3 — Adapter adoption bar (F09-F11).
- [ ] M4 — External validation (F13; F12 is done).
- [ ] M5 — Capability catalog + protected ship skeleton (F14-F17).
- [ ] M6 — Policy truth tables + native SDD model (F18-F20).
- [ ] M7 — Readiness + generated-view currency (F21-F22).
- [ ] M8 — Generated-product and surface-domain checks (F23-F24).
- [ ] M9 — Cost/cache/provenance + release gates (F25-F26).
- [ ] M10 — Human projections over stable reports (F27).

1. **M1 — First useful product (F01–F06). ✅ Reached.** Pure kernel + evidence +
   JSON explanation, packed (`FS.GG.Governance.Kernel.0.1.1.nupkg` →
   `~/.local/share/nuget-local/`), zero heavy deps. Satisfies org G2/G3 "narrow
   tool" and the project-scope "first useful product."
2. **M2 — Light routing + effects edge (F07–F08). ✅ Reached.** Explainable,
   light-by-default routing (F07) and the `FS.GG.Governance.Host` MVU effects edge
   (F08) that senses artifacts, runs the pure kernel, dispatches agent reviews, and
   freezes verdicts as cache-keyed evidence — the first Elmish/MVU boundary feature,
   zero new dependency.
3. **M3 — Adoption bar (F09–F11). ✅ Reached.** SPI + composition root + two
   unrelated domains (Spec Kit, design-system). The kernel is now demonstrably a
   library, not a platform.
4. **M4 — Tool + external validation (F12–F13). In progress.** Optional CLI is
   implemented, tested, and packed as `FS.GG.Governance.Cli.0.1.1.nupkg` (F12);
   run against an external repo from the outside next (F13, org G4); begin the org
   G5 adoption decision.
5. **M5 — Capability catalog + protected ship skeleton (F14-F17). Planned.** This
   is the first slice from the capability design report: `.fsgg` source schemas,
   git/CI facts, capability routing, gate registry, route traces, and
   `ship --mode gate --profile standard --json` as a deterministic branch gate.
6. **M6 — Policy truth tables + native SDD model (F18-F20). Planned.** Profiles,
   maturity, effective severity, native SDD bootstrap, and normalized work-model
   conflict rules make governance executable without Markdown/schema drift.
7. **M7 — Readiness + generated-view currency (F21-F22). Planned.** Route,
   contract, explain, evidence, audit, and summary artifacts become deterministic
   generated views, and `refresh` owns regeneration/currency.
8. **M8 — Generated-product and surface-domain checks (F23-F24). Planned.**
   Capability catalog expansion adds generated product, package/API,
   docs/examples, skills, design/rendering, samples, and release surfaces with
   deterministic checks first.
9. **M9 — Cost/cache/provenance + release gates (F25-F26). Planned.** Expensive
   evidence becomes cacheable only through freshness keys, command-run records,
   environment classes, and release-specific pack/publish/provenance gates.
10. **M10 — Human projections (F27). Planned.** Plain text, Spectre.Console,
    watch, and optional TUI surfaces render the same report objects used by JSON.

## 8. Driving it with Spec Kit

For each feature, in order:

1. `/speckit-specify` — author `specs/NNN-slug/spec.md`: user-visible outcome,
   scope, **Tier**, public-API impact, verification approach.
2. *(optional)* `/speckit-clarify` — resolve unknowns.
3. `/speckit-plan` — design: the `.fsi` contract, the FSI sketch, the
   project/layout deltas, MVU model where applicable, and which decisions (§6)
   this feature locks.
4. `/speckit-tasks` — author `tasks.md` (FSI sketch → semantic tests → impl →
   `.fsi`/baseline update → pack, in that constitutional order).
5. `/speckit-implement` — implement against the stable signature and passing
   tests; disclose any synthetic evidence per Principle V.
6. `/speckit-analyze` before merge as an advisory cross-artifact check.

Feature IDs above (`001`…`027`) are proposed `specs/` slugs; renumber freely as
features split or merge. Keep the dependency intent stable even if a later slice
splits a large capability feature into multiple specs.

## 9. Risks & deferrals

- **LLM-judge realities** (#1–#3, #5) are the main exposure; they are quarantined
  to F04/F08 and not on the M1 critical path — the first useful product has no
  agent dependency at all.
- **Scope creep into a platform.** Each feature must keep the kernel
  domain-vocabulary-free and deletable; the adoption bar (M3) is the explicit
  check that generality is real, not assumed.
- **Adapter realism.** The design-system and Sojourn examples are sketches;
  expect probe bodies (the `fun facts -> … Met` parts) to be the bulk of real
  adapter work and to need their own fixtures.
- **Confluence edge cases** (#4) are theory-level today; they become real only if
  a rule ever needs aggregation or negation over still-being-derived facts — keep
  the kernel inside the safe fragment by construction.
- **Schema drift replaces Markdown drift.** F14-F22 must treat schema versioning,
  conflict diagnostics, and generated-view freshness as first-class gates, not
  documentation conventions.
- **Protected-boundary value arrives too late.** F17 is intentionally earlier than
  the full lifecycle command suite; do not defer `ship` until every domain check
  exists.
- **Policy dials become inscrutable.** F18 is not complete without golden
  enforcement truth tables and representative JSON snapshots for every
  enforcement-affecting combination.
- **Generated products become coupled to the monorepo.** F19 and F23 must prove a
  generated product can run governance locally from its declared `.fsgg` catalog
  and package/tooling inputs.
- **Provenance overclaims.** F25-F26 emit useful metadata first and reserve formal
  SLSA/in-toto claims for explicit, separately verified compliance.
