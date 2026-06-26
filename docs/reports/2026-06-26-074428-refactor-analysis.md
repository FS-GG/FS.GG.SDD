---
title: Refactor analysis — code smells and extraction opportunities
category: Reports
date: 2026-06-26
generated: 2026-06-26 07:44:28 CEST
scope: src/ (FS.GG.SDD.Artifacts, .Commands, .Cli, .Validation)
---

# Refactor Analysis: FS.GG.SDD

A thorough, evidence-based pass over the `src/` tree for code smells —
god modules, superfluous `private` modifiers, nullness/incomplete-match
warnings, and duplication that could be extracted. Every claim below is
cited with `File.fs(Lstart-Lend)` and was measured against `main` on
2026-06-26 (build `0.2.0`, .NET SDK 10.0.301).

This is an analysis, not a change. Nothing in `src/` was modified. The
suite (437 tests) passes today and the smells below are maintainability /
latent-risk issues, not active defects.

## Method

- Line counts: `wc -l` over `src/**/*.fs(i)` excluding `obj/`.
- Warnings: clean rebuild (`dotnet build -c Release --no-incremental`),
  deduplicated to unique `file(line,col)` sites (raw emitted counts are
  inflated ~2× because a dependency's warnings re-emit in every
  referencing project).
- Duplication / structure: targeted reads of the two god modules plus the
  report/serialization layers.

## Scorecard

| Metric | Value |
|---|---|
| `src` F# implementation LOC | 17,413 across 22 `.fs` files |
| LOC in the two largest files | **9,999 (57%)** — `CommandWorkflow.fs` + `LifecycleArtifacts.fs` |
| Largest single module | `CommandWorkflow.fs` — 6,838 lines, one flat module, 227 bindings, **7-line `.fsi`** |
| Unique nullness warnings (FS3261) | **290** in `src` (+ ~9 in tests) |
| Unique incomplete-match warnings (FS0025) | **4** (all in `LifecycleArtifacts.fs`) |
| Redundant `private` modifiers | **82** across 9 `.fsi`-guarded files |
| `failwith` / partial-fn escapes in `src` | ~10 |
| `<TreatWarningsAsErrors>` | `false` — warnings accrue silently |

---

## 1. God modules (Severity: High)

### 1.1 `CommandWorkflow.fs` — 6,838 lines, one flat module

39% of all `src` LOC. The `.fsi` exposes exactly two values:

```
// CommandWorkflow.fsi — the entire public surface
val init:   request: CommandRequest -> CommandModel * CommandEffect list
val update: msg: CommandMsg -> model: CommandModel -> CommandModel * CommandEffect list
```

Everything else (225 of 227 bindings) is internal but lives in a single
`module CommandWorkflow` with no nested-module structure — the only nested
declarations are 8 `module X = FS.GG.SDD.Artifacts.Y` aliases
(`CommandWorkflow.fs:20-27`).

Logical sections that are natural file/module boundaries:

| Section | Approx. range | Content |
|---|---|---|
| Config & init effects | 19–97 | schema aliases, `initEffects` |
| Path helpers & read effects | 98–270 | path constants, 12× `*ReadEffects` |
| Diagnostic & prerequisite validators | 271–3714 | ~49 prerequisite/validator bindings, per-artifact parse helpers |
| Per-command handlers (`compute*Plan`) | 3715–5982 | charter→ship handlers, 20–617 lines each |
| Cross-cutting handlers | 5983–6688 | `computeAgentsPlan` (272 ln), `computeRefreshPlan` (434 ln) |
| Orchestration / MVU | 6689–6838 | `nextLifecycleEffects`, `init`, `update` |

**Recommendation.** Split into a `CommandWorkflow` facade (keeps
`init`/`update`) over internal modules: `Prerequisites`, `Parsing`,
`Handlers.Specify`/`.Plan`/… (or one `Handlers` module with per-stage
submodules), and `ViewRendering`. The 7-line `.fsi` already proves the
public contract is tiny, so this is a pure internal reorganization with
near-zero blast radius.

### 1.2 `LifecycleArtifacts.fs` — 3,161 lines, ~45 types + 10 artifact parsers

One flat module mixing every lifecycle artifact's **type definitions** and
its **parsers** (project/sdd/agents config, spec, clarification, checklist,
plan, tasks, analysis, evidence, verification, ship, guidance). The 722-line
`.fsi` is the contract; the module would split cleanly per artifact family:

| Family | Types (approx.) | Parsers |
|---|---|---|
| Infra (project/sdd/agents) | `17-43` | 3 |
| Specification | `54-81` | FrontMatter `1018-1045`, Facts `1170-1225` |
| Clarification | `92-152` | `1226-1259`, `1413-1463` |
| Checklist | `153-198` | `1464-1499`, `1609-1667` |
| Plan | `200-276` | `1668-1898`, `1899-1951` |
| Task | `278-350` | `2069-2146`, `2147-2161` |
| Analysis / Evidence / Verify / Ship | `352-730` | view parsers `2376-2786` |

**Recommendation.** Promote each family to its own file under a
`LifecycleArtifacts/` folder (F# compiles ordered files; keep current
ordering). This also localizes the 144 nullness sites (§3) and the 4
incomplete matches (§4) to the Analysis/Verify/Ship parser file.

---

## 2. Superfluous `private` modifiers (Severity: Low — pure noise)

**82 `private` modifiers** appear across 9 files, and **every one of those
files has an `.fsi` signature**:

| File | `private` count | Has `.fsi`? |
|---|---|---|
| `ValidationRunner.fs` | 34 | yes |
| `ReleaseContract.fs` | 20 | yes |
| `Rendering.fs` (Cli) | 8 | yes |
| `WorkModel.fs` | 7 | yes |
| `GovernanceHandoff.fs` | 5 | yes |
| `ValidationHarness.fs` | 3 | yes |
| `LifecycleArtifacts.fs` | 3 | yes |
| `SchemaVersion.fs` / `CommandWorkflow.fs` | 1 each | yes |

When a module is backed by an explicit `.fsi`, the signature file is the
sole arbiter of visibility — any binding not re-exported there is already
inaccessible. Marking such a binding `private` in the `.fs` is redundant
(e.g. `CommandWorkflow.fs:5336 let private parseShipReadinessFacts` is
already invisible because the 7-line `.fsi` omits it).

**Recommendation.** Strip `private` from `.fs` bindings in `.fsi`-guarded
modules. It is harmless but misleading — it implies a visibility decision
the signature file has already made. Zero behavior change; mechanical.

---

## 3. Nullness warnings (Severity: Medium)

`Directory.Build.props` sets `<Nullable>enable</Nullable>` with
`<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`, so **290 unique
FS3261 sites** accumulate unaddressed in `src` (raw emitted count 952 is
the same sites re-counted per referencing project). Distribution:

| File | Unique FS3261 sites |
|---|---|
| `LifecycleArtifacts.fs` | 144 |
| `WorkModel.fs` | 53 |
| `ReleaseContract.fs` | 28 |
| `CommandWorkflow.fs` | 17 |
| `ValidationContracts.fs` | 14 |
| `GenerationManifest.fs` | 10 |
| `SchemaVersion.fs` | 8 |
| `Identifiers.fs` / `ArtifactRef.fs` | 4 |
| tests | ~9 |

These cluster around `System.Text.Json` boundaries (`JsonElement` /
`GetString()` returning `string | null`) and string handling — note the
defensive `if isNull value then ""` idioms already sprinkled in
(`LifecycleArtifacts.fs:2451`), which are the manual workaround for exactly
these warnings.

**Risk.** 290 ignored warnings create alert-blindness: a *new* nullness or
correctness warning is invisible in the noise. The codebase otherwise
prizes totality and determinism, so this is the one place discipline has
slipped.

**Recommendation (staged).**
1. Wrap the JSON-access helpers (`jsonString`, `jsonRequiredString`,
   `jsonInt`, …) once so null handling is centralized and the call sites
   become null-clean — this likely clears the bulk of the 144 in
   `LifecycleArtifacts.fs` and 53 in `WorkModel.fs` at a few dozen helper
   edits.
2. Once the count is near zero, flip `TreatWarningsAsErrors` to `true` (or
   `WarningsAsErrors=FS3261;FS0025`) so regressions can't reaccumulate.

---

## 4. Incomplete pattern matches (Severity: Medium — latent runtime throw)

**4 FS0025 sites**, all the same shape, in the JSON view parsers:
`LifecycleArtifacts.fs(2385)`, `(2551)`, `(2655)`, `(2751)`.

Each matches `compatibility.Version, compatibility.Status`:

```fsharp
match compatibility.Version, compatibility.Status with
| Some schema, Current
| Some schema, Deprecated -> ...                 // happy path
| _, Malformed   -> Error ...
| _, Unsupported -> Error ...
| _, Future      -> Error ...
// (None, Current) and (None, Deprecated) are UNHANDLED
```

The `(None, Current)` / `(None, Deprecated)` combinations have no arm, so
the match is non-exhaustive and would raise `MatchFailureException` at
runtime. It is "safe" today **only** because `SchemaVersion.classifyRaw` is
assumed never to return a `Current`/`Deprecated` status with a `None`
version — an invariant the types do not encode and nothing enforces.

**Recommendation.** Make it total: add an explicit `| None, _ ->` error arm
(treat a current-status-but-unparsed-version as malformed), or change
`classifyRaw` to return a single sum type that makes the impossible state
unrepresentable. The four parsers are near-identical (§5.3), so fixing the
shared skeleton fixes all four at once.

---

## 5. Duplication & extraction opportunities (Severity: Medium–High)

### 5.1 Per-command handlers share one skeleton (High)

The 12 `compute*Plan` handlers all follow the same template: guard on
`WorkId` → accumulate diagnostics (project + duplicate + prerequisite
cascade) → `hasBlocking = diagnostics |> List.exists isError` → emit
effects only when not blocking → return `(diagnostics, artifacts, views,
effects)`. The prerequisite cascade is hand-rolled per stage
(`computeAnalyzePlan` 5-deep at `4018-4027`; `computeEvidencePlan` 6-deep
at `4626-4656`).

Several are very long, amplifying the duplication:

| Handler | Range | Lines |
|---|---|---|
| `computeVerifyPlan` | `5095-5639` | **544** |
| `computeRefreshPlan` | `6255-6688` | **434** |
| `computeShipPlan` | `5640-5982` | 343 |
| `computeAnalyzePlan` | `4003-4289` | 286 |
| `computeAgentsPlan` | `5983-6255` | 272 |

**Recommendation.** Extract a prerequisite **combinator** (an ordered list
of `WorkModel -> Diagnostic list` checks the engine folds, short-circuiting
on the first blocker) and a `runHandler` shell that does the guard /
hasBlocking / effect-gating boilerplate, leaving each stage to supply only
its artifact-build and view-render functions. This is the single highest-
leverage refactor: it shrinks the god module (§1.1) and removes the
copy-paste prerequisite chains in one move.

### 5.2 Diagnostic constructors collapse to a builder (Medium)

`CommandReports.fs` has ~61 diagnostic functions in families that are
structurally identical except for an id string, message, correction, and
related-ids list:

| Family | Count | Examples (lines) |
|---|---|---|
| `missing*` | 13 | `missingProjectConfig` (67), `missingSddConfig` (85) |
| `malformed*` | 12 | `malformedProjectConfig` (76), `malformedSddConfig` (94) |
| `unknown*` | 7 | `unknownSpecificationReference` (222) |
| `stale*` | 6 | — |
| `duplicate*` | 6 | `duplicateWorkId` (121), `duplicateSpecificationId` (204) |
| `unsafe*` / `failed*` | 8 | — |

Each body is one `commandDiagnostic "id" Error pathOpt "message"
"correction" relatedIds` call.

**Recommendation.** Keep the named functions as thin call-sites if the
ids/messages are part of the contract, but route them through one generic
`diagnostic kind stage pathOpt messageId relatedIds` so the
severity/path/sort conventions live in exactly one place. ~50 LOC and a
guarantee that all diagnostics share one shape.

### 5.3 JSON view-parser skeleton (Medium)

The four view parsers (`parseAnalysisView` 2376, `parseVerificationView`
2542, `parseShipView` 2646, `parseGeneratedAgentGuidance` 2742) repeat the
same 7-step skeleton: parse `JsonDocument` → read `schemaVersion` →
`classifyRaw` → match version/status (the §4 incomplete match) → parse
identity fields → map+sort each array field → duplicate
Malformed/Unsupported/Future error arms (`2441-2446`, `2607-2610`,
`2711-2714`, `2779-2784`).

**Recommendation.** Extract `parseJsonView` taking the per-artifact
record-builder and entry-parsers, with the schema-classification and the
three error arms handled once. Removes ~70 LOC **and** the four incomplete
matches collapse to a single total match.

### 5.4 Serialization helper overlap (Low–Medium)

`CommandSerialization.fs` and `Serialization.fs` independently implement
overlapping JSON writers: `writeOutputDigest` (≈100% duplicate),
`writeDiagnostic` (`CommandSerialization.fs:49-62` vs
`Serialization.fs:169-182`, both full impls), plus `writeStringList` /
`writeDigest` / `writeLocation` variants that differ only by sort behavior
or an extra name parameter.

**Recommendation.** Hoist the truly identical writers into a shared
`Serialization` core and parameterize the variants (pass the sort flag /
field name) rather than forking. ~40 LOC.

### 5.5 Scattered micro-duplication (Low)

- View-state construction `generatedViewState path … Blocked ids` copy-
  pasted 6+ times (`CommandWorkflow.fs:3755, 4062, 4695`).
- `SchemaVersionModule.sha256Text` called ~10× with no wrapper.
- Three+ source-snapshot builders replicate stale-check logic
  (`renderEvidenceSourceSnapshot` 4525, `renderTaskSourceSnapshots` 2741).

These fall out naturally once §5.1/§5.3 land.

---

## 6. Minor: partial-function escapes (Severity: Low)

~10 `failwith` / `Result.defaultWith failwith` sites convert `Result.Error`
to exceptions inside otherwise-total code: `ValidationRunner.fs:638`
(`"report not built"`), `ReleaseContract.fs:266,451`,
`CommandWorkflow.fs:2502,2507,2512,4290,4329`, `SchemaVersion.fs:166`. Most
are post-validation "can't happen" invariants, but they sit oddly against
the codebase's total-function discipline and discard context (the
`2502-2512` task-id/ref conversions carry no `workId`/path in the thrown
message).

**Recommendation.** Where the value is genuinely unreachable, prefer an
explicit `invalidOp`/`failwithf` with context; where it can fail on bad
input, thread the `Result` to a diagnostic instead of throwing.

---

## 7. Refactor roadmap

Progress markers (status legend, matching
`docs/initial-implementation-plan.md`): 🟢 / ✅ complete · 🟡 in progress
(started; not landed) · 🔴 not started · ⬜ optional / deferred. The
2026-06-26 analysis baseline started every row at 🔴; **R3 and R4 have since
landed** (see below). Update the marker as each refactor lands and link its evidence
(PR / spec readiness).

Suggested sequence: **R3 → R4 → R1 → R2** (structure first so the handler
extraction has somewhere to live), then **R5**, then the low-risk
**R6 / R7** cleanups. Each row is independently shippable and guarded by the
existing 437-test suite. R1/R2/R4–R7 additionally hold the public `.fsi`
contract and deterministic JSON output byte-stable. **R3 was shipped under a
relaxed gate** (stakeholder decision recorded in
`specs/022-split-lifecycle-artifacts/plan.md`): because there are **no external
consumers**, the single binding criterion is **the existing test suite passes** —
the per-family module-qualifier `.fsi` reshape (and its surface-baseline regen)
is permitted, and byte-identical artifact output is not separately required.

| ID | Status | Refactor | Refs | Severity | Effort | Risk | Payoff |
|---|---|---|---|---|---|---|---|
| R1 | 🔴 | Extract prerequisite combinator + `runHandler` shell | §5.1 | High | M–L | Low | Shrinks god module, kills handler copy-paste |
| R2 | 🔴 | Split `CommandWorkflow.fs` into facade + internal modules | §1.1 | High | M | Very low (7-line `.fsi`) | Navigability |
| R3 | ✅ | Split `LifecycleArtifacts.fs` per artifact family | §1.2 | High | M | Very low | Navigability, localizes §3/§4 |
| R4 | ✅ | Extract `parseJsonView`, making the 4 matches total | §5.3 §4 | Med | S | Low | Removes latent runtime throw + ~70 LOC |
| R5 | 🔴 | Centralize JSON null-access helpers, then enable `WarningsAsErrors` | §3 | Med | M | Low | Clears ~290 warnings, prevents regression |
| R6 | 🔴 | Collapse diagnostic builder + unify serializers | §5.2 §5.4 | Med | S | Low | ~90 LOC, one shape |
| R7 | 🔴 | Strip redundant `private`; fix `failwith` context | §2 §6 | Low | S | None | Noise removal |

### Status detail

- 🔴 **R1 — prerequisite combinator + `runHandler` shell.** Not started.
  Done when the 12 `compute*Plan` handlers share one guard/`hasBlocking`/
  effect-gating shell and an ordered prerequisite combinator; no per-stage
  copy-pasted cascade remains.
- 🔴 **R2 — split `CommandWorkflow.fs`.** Not started. Done when the module
  is a facade over internal `Prerequisites` / `Parsing` / `Handlers` /
  `ViewRendering` modules and no single file exceeds ~1,500 lines; `.fsi`
  unchanged (`init`/`update`).
- ✅ **R3 — split `LifecycleArtifacts.fs`.** Landed via
  `specs/022-split-lifecycle-artifacts/`. **Gate (relaxed):** the original
  "722-line `.fsi` contract is unchanged" criterion was dropped during planning —
  with no external consumers, the binding gate is **build + the existing 437-test
  suite pass**, and byte-identical output is not separately required (plan.md
  stakeholder decisions overriding FR-002/FR-003/FR-005). **Done:** each artifact
  family now lives in its own `[<AutoOpen>]` module file under
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/` (largest 389 lines, down from
  3,161), fronted by a shared `Internal` helper module and `Core` types module,
  with `WorkItem` aggregating last. The monolith `LifecycleArtifacts.fs`/`.fsi`
  are removed; the aggregate public **member set** is preserved (the 722-line
  `.fsi` sliced per family; the surface baseline was regenerated for the
  module-qualifier rename only — member names unchanged). All 437 tests pass, and
  the 290 FS3261 / 4 FS0025 warning sites relocated unchanged (concentrating in
  `Analysis.fs`/`Verify.fs`/`Ship.fs`).
- ✅ **R4 — `parseJsonView` + total matches.** Landed via
  `specs/023-extract-json-view-parser/`. **Done:** the four view parsers
  (`parseAnalysisView`/`parseVerificationView`/`parseShipView`/
  `parseGeneratedAgentGuidance`) route through one internal `parseJsonView`
  skeleton in `LifecycleArtifacts/Internal.fs`, each supplying only its
  artifact-specific `build` callback. The shared `version, status` match is now
  **total** — the four `(None, Current/Deprecated)` combos fold into the
  malformed-schema arm — clearing all **4 FS0025** sites (now 0) and removing the
  latent `MatchFailureException`. `dotnet build -c Release` is green with zero
  FS0025; FS3261 unchanged (952 lines); all **437** prior tests pass plus **1**
  new required totality assertion (438 total); no public `.fsi` diff; net `src`
  shrinks ~70 LOC (66 ins / 80 del across 5 files). One internal-only deviation:
  the skeleton takes `path`/`text` strings rather than a `FileSnapshot` record,
  because `FileSnapshot` (in `Core.fs`) compiles after `Internal.fs` — behavior
  is byte-identical and no public surface changes.
- 🔴 **R5 — null-clean JSON helpers + `WarningsAsErrors`.** Not started.
  Done when FS3261 count is ~0 and `Directory.Build.props` sets
  `WarningsAsErrors` (or `WarningsAsErrors=FS3261;FS0025`).
- 🔴 **R6 — diagnostic builder + serializer unification.** Not started.
  Done when diagnostics route through one generic builder and the duplicate
  JSON writers live in a single shared module.
- 🔴 **R7 — `private` + `failwith` cleanup.** Not started. Done when no
  `.fsi`-guarded `.fs` carries a redundant `private` and each remaining
  `failwith` is either total/unreachable-by-construction or replaced by a
  threaded diagnostic.

**Aggregate:** 2 / 7 complete · 0 in progress · 5 not started.

## Appendix: what is *not* a problem

- **Layering** is clean: `Artifacts → Commands → Cli`/`Validation`, one-way
  deps, no cycles.
- **`.fsi`-first discipline** holds on every public module (only the
  `Program.fs` entry point lacks one — correct).
- **Determinism** holds — digests over identical source trees are
  byte-stable; no clock/ordering leakage observed.
- The smells above are **maintainability and latent-risk**, not active
  defects: the suite is green and the CLI runs the full lifecycle without
  Governance installed.
