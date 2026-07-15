---
title: Code and architecture review
category: Reports
date: 2026-07-15
generated: 2026-07-15 11:06:24 CEST
scope: full repo — src/, tests/, .github/, scripts/, docs/, build props
baseline: main @ 6fe63df, .NET SDK 10.0.302
---

# Code & Architecture Review: FS.GG.SDD

A comprehensive, evidence-based review of the repository at `main` (`6fe63df`),
covering architecture and layering, correctness in the Artifacts/Contracts
parsing layer, the command/CLI/validation layer, and the test suite plus
CI/build/docs hygiene. Every claim is cited `File.fs:line` and was checked
against source on 2026-07-15; crash-class findings marked **[verified]** were
reproduced empirically against the built assemblies.

This is an analysis, not a change — nothing in `src/` was modified. It was
produced by four parallel review passes (architecture/layering,
Artifacts+Contracts correctness, Commands/Cli/Validation quality, and
tests/CI/docs), each with full file reads, targeted greps, and — for the
highest-risk findings — empirical probes.

## Baseline

| Metric | Value |
|---|---|
| `src` F# LOC | 41,023 across 96 `.fs` + 59 `.fsi` files (5 projects) |
| `tests` F# LOC | 36,871 (6 test projects) |
| Build | clean — **0 warnings, 0 errors** (`dotnet build -c Release`) |
| Tests | **1,763 passed / 0 failed / 4 skipped** (network-gated acceptance, as designed) |
| Per-PR CI test execution | **full solution** — `gate.yml:73` runs `dotnet test` on every PR |
| Project dependency graph | strictly acyclic; typed effect DU; MVU core verifiably pure |
| Largest source file | `EarlyStageAuthoring.fs` — 1,738 lines |

## Executive summary

**The repository is in excellent shape and has measurably improved since the
2026-07-02 review.** Three of that review's four headline findings are now
fixed and verified:

1. **CI now runs the full test suite on every PR.** `gate.yml:72-73` runs
   `dotnet test FS.GG.SDD.sln` on every push/PR to `main`; all six test
   projects gate a merge. (Was: build-only, tests on release events only.)
2. **Hand-authored YAML no longer crashes on ordinary malformed input.**
   `Internal.parseYaml` now catches `YamlException` (`Internal.fs:62`) and
   `SchemaVersion.parse` uses `Int32.TryParse(NumberStyles.None)` — both
   verified against the built assembly to return diagnostics, not throw.
3. **`RunProcess` now has a kill-on-timeout.** `CommandEffects.fs:136-146`
   bounds every external process (default 600 s, `FSGG_SDD_PROCESS_TIMEOUT_MS`
   override), kills the tree on expiry, and synthesizes fail-closed exit 124.
4. The prose keyword-sniffing bug (`(unanswered)` classified as answered) is
   fixed — state matching now uses a word-boundary regex `containsWord`
   (`Internal.fs:288-289`), which cannot match inside `unanswered`.

The architecture is exemplary and **structurally enforced**, not merely
claimed: the layer graph is strictly acyclic, the MVU pure core has zero
IO/clock/env/nondeterminism calls outside the `CommandEffects.fs` edge, Spectre
is quarantined to the CLI host, and every load-bearing CLAUDE.md boundary claim
spot-checked (single mirror authority, `doctor` read-only, lexical
root-containment, no provider tokens in generic SDD, `nextLifecycleCommand =
None` for cross-cutting commands) holds in code.

The remaining open items, highest-leverage first:

1. **[High, verified] Deeply-nested YAML aborts the process** with an
   uncatchable `StackOverflowException` — the one crash class the current
   design cannot catch, and a direct violation of the malformed-input-is-exit-1
   doctrine.
2. **[Medium] `lint` classifies defects by substring-matching parser prose
   across an assembly boundary** — a reworded message silently drops the
   defect and reports `Clean`. Same class of prose-driven-state bug as the one
   just fixed, one level up.
3. **[Medium] Quantified duplication** — 11 near-identical identity-mismatch
   diagnostic constructors and a copy-pasted view-load/parse block (~150 lines
   collapsible), a drift hazard.
4. Everything else is Low: a narrow post-kill hang window, dead contract
   surface, doc drift (stale test counts, README missing `surface`), and
   `.fsi`/module-size polish.

No Critical findings. No new High-severity defect in the command/CLI layer.

---

## 1. Architecture & layering — exemplary

**Verdict: the layering is a genuine strength, mechanically verifiable.** No
Critical or High findings.

**Strictly acyclic dependency graph** (derived from `ProjectReference`s):

```
FS.GG.Contracts      (FSharp.Core only — BCL-only leaf)
   ↑
FS.GG.SDD.Artifacts  (Contracts + YamlDotNet)
   ↑
FS.GG.SDD.Commands   (Contracts + Artifacts)
   ↑
FS.GG.SDD.Validation (Artifacts + Commands — no Spectre, no Governance)
   ↑
FS.GG.SDD.Cli        (all four + Spectre.Console — the only Spectre ref)
```

Clean linear layering, no back-edges (`FS.GG.SDD.Validation.fsproj` forward-only
onto Artifacts+Commands; Cli sole top).

**The MVU pure core is genuinely pure.** A precise grep for `File.`/`Directory.`
method calls, `Console.`, `DateTime.Now/UtcNow`, `Environment.*`,
`Guid.NewGuid`, `Random`, `Process.Start` across `FS.GG.SDD.Commands` returns
**zero** hits outside `CommandEffects.fs`. All nondeterminism/IO is confined to
the edge: `Guid.NewGuid` (`CommandEffects.fs:50`),
`Environment.GetEnvironmentVariable` (`:144`), and all `File.`/`Directory.`
calls. The pure core plans a typed `CommandEffect` DU
(`CommandTypes.fsi:837-847`) and never performs the effect.

**Spectre.Console is quarantined to the CLI host.** The only real
`PackageReference Include="Spectre.Console"` is
`FS.GG.SDD.Cli.fsproj:45`; the only `AnsiConsole`/`IAnsiConsole` usage is in
`FS.GG.SDD.Cli/Rendering.fs` and `RegistryValidate.fs`. `Validation` carries no
Spectre reference — its `--rich` degrades in-library
(`ValidationRunner.fs:250`).

**CLAUDE.md boundary claims hold in code** (spot-checked):
- `nextLifecycleCommand = None` for all cross-cutting commands
  (`CommandTypes.fs:965-985`: Agents, Refresh, Scaffold, Doctor, Upgrade, Lint,
  Surface, Help).
- `doctor` plans only `ReadFile`/`EnumerateDirectory`; `upgrade` is the sole
  mutator emitting `Confirm`+`RunProcess`+`WriteFile` — the ADR-0009 split.
- `surface` lexical root-containment tests the **raw** string before any
  normalization (`PathContainment.escapesRoot`, `PathContainment.fs:6-11`),
  enforced at exactly one place (`Foundation.fs:713-726`).
- `SkillMirror` is single-authority (`SkillMirror.fsi:8`); the
  `providerWroteSddTree` guard reserves `.claude`/`.codex` and the
  `.agents/skills/fs-gg-sdd-` namespace (`HandlersScaffold.fs:60-72`).
- No provider/rendering token in generic SDD — grep for `FS.GG.Rendering`,
  `Fable`, `Feliz`, template ids, nuget URLs finds none; `TemplateId` is a
  descriptor field forwarded verbatim (`HandlersScaffold.fs:364,393`).
- Scaffold provenance is `SchemaVersion = 1` with `mirroredPaths`
  (`ScaffoldProvenance.fs:47,120`).

### Findings

- **[Low] `.fsi` convention is inconsistent for internal submodules.** 25
  submodule files under `CommandReports/`/`CommandWorkflow/` have no paired
  `.fsi` (e.g. `Foundation.fs`, `HandlersScaffold.fs`,
  `DiagnosticConstructors.fs`) while three siblings do (`LintEngine.fsi`,
  `ProcessSkillManifest.fsi`, `LifecycleFooter.fsi`). Not an encapsulation
  leak — every one declares `module internal` under `...Commands.Internal`,
  closed by the aggregating `CommandWorkflow.fsi`/`CommandReports.fsi`. Purely
  stylistic drift. *Pick one convention and document it.*
- **[Low] `open System.IO` in 14 pure-core workflow modules.** Verified used
  only for pure `Path.*` string ops (no `File.`/`Directory.` calls resolve),
  so the core stays pure — but a blanket open puts `File`/`Directory` one
  keystroke away in the module that must never call them. *Consider `open type
  System.IO.Path` or a `Path` alias to keep the effectful surface out of scope.*

---

## 2. Artifacts & Contracts — one open crash class

Scope: the schema types, YAML/JSON codecs, and lifecycle artifact models that
parse **hand-authored** input. Doctrine under test: malformed authored input
must yield an exit-1 diagnostic, never a crash.

### 2.1 [High, verified] Deeply-nested YAML aborts the process (uncatchable StackOverflow)

`Internal.fs:47-65` — `parseYamlDocument` wraps `stream.Load reader` in `try …
with :? YamlDotNet.Core.YamlException`, the single correct chokepoint for
ordinary malformed YAML (confirmed the only `YamlStream` in either project, so
every authored YAML — `evidence.yml`, `tasks.yml`, `agents.yml`, all markdown
front-matter, the codec `decode` path — flows through it). But YamlDotNet 18.1.0
parses nested flow collections **recursively with no depth limit**, so a
hand-authored document of the form `[[[[[…` blows the CLR stack. A
`StackOverflowException` is uncatchable in .NET — it bypasses the `try/with`
and terminates the process.

**Verified empirically** against the pinned YamlDotNet 18.1.0 `net10.0`
assembly: a document of ~50,000 `[` characters (~50 KB) produced `Stack
overflow.` and process abort **exit 134 (SIGABRT)**; a post-`try/with`
`printfn` never executed. The same `stream.Load` call underlies every lifecycle
parser, so this reproduces through any of them. (Contrast: duplicate key, tab
indentation, unterminated quote, dangling alias all raise a `YamlException`
subclass and are caught cleanly.)

**Impact:** a ~50 KB authored `.yml`/front-matter file crashes `fsgg-sdd` with a
hard abort instead of the promised exit-1 diagnostic — a doctrine violation and
a trivial local DoS. A wider `catch` cannot defend it (the exception is
uncatchable); only a **byte-budget / nesting-depth pre-scan before
`stream.Load`** can. Severity is High as a real doctrine violation, tempered by
the input being pathological rather than a plausible typo.

### 2.2 Resolved & clean

- **[Resolved, verified]** Prior "`parseYaml` does not catch YamlException" —
  fixed at `Internal.fs:55-65`, returns structured `YamlMalformed(msg,line,col)`
  → `Diagnostics.malformedYaml`.
- **[Resolved, verified]** Prior "`SchemaVersion.parse` OverflowException" —
  fixed at `SchemaVersion.fs:37-68` via `Int32.TryParse(NumberStyles.None)`;
  `parse "99999999999999999999"` and `"2147483648"` return `Error`. Sibling
  `Fsgg.Version.tryParse` (`Version.fs:10-35`) uses the same guard.
- **[Low] The YAML catch is type-narrow** (`Internal.fs:62`, only
  `YamlException`). Correct today — every YamlDotNet authored-input error
  derives from it — but a future non-`YamlException` path would escape to the
  same crash outcome as 2.1. Defensive, not currently reproducible.
- **Clean:** every authored numeric read uses `Int32.TryParse` with
  failure→None (`ArtifactCodec.fs:159`, `Evidence.fs:396`, `Task.fs:226`,
  `Checklist.fs:158`, `Plan.fs:219`, …); all five JSON parse sites are
  `try/with`-wrapped; `invalidArg`/`failwithf` fire only on tool-constructed
  (not authored) paths; no `List.head`/`Option.Value`/map-indexer on authored
  data.
- **Determinism is clean:** no `DateTime`/`Guid`/`Random`/`Environment.*` in
  serialized output; `Map`/`Set` enumerate in key order plus explicit
  `List.sortBy`; `sha256Text` normalizes CRLF→LF before hashing
  (`SchemaVersion.fs:168-178`), so digests are platform-stable.

---

## 3. Commands / CLI / Validation — solid, two Medium items

Both prior-review criticals for this layer (RunProcess timeout; `(unanswered)`
misclassification) are **fixed and verified** (see Executive summary). No new
Critical/High correctness defect.

### Findings

- **[Medium] `lint` prose-sniffs parser messages across an assembly boundary.**
  `LintEngine.classify` (`LintEngine.fs:101-114`) maps the generic parser id
  `workModelInconsistent` to a grammar class by substring-matching the human
  message: `msg.Contains "front matter is incomplete"` (`:101`) and
  `msg.Contains "missing a required stable id" && (…"functional requirements" ||
  …"acceptance")` (`:109-110`). Those exact strings are authored in a **different
  assembly** (`Artifacts/LifecycleArtifacts/{Specification,Checklist,…}.fs`).
  No compile-time link: if a parser author rewords a message, `classify` falls
  through to `None` (`:114`) and the defect is **silently dropped** — `lint`
  reports `Clean` (exit 0) on a defective artifact. Same class of
  prose-driven-state bug just fixed, one level up. *Emit a structured
  discriminator (stable sub-id / typed defect field) the parsers own and lint
  keys on; at minimum pin the coupling with a shared literal.*

- **[Medium] Quantified duplication (drift hazard, ~150 lines).**
  - 11 near-identical `*IdentityMismatch` constructors in
    `DiagnosticConstructors.fs` (charter `:210`, specification `:242`,
    clarification `:305`, checklist `:360`, plan `:428`, tasks `:524`, analysis
    `:612`, evidence `:655`, verify `:920`, ship `:1010`, agents `:1040`) differ
    only by an id string and one noun. *Collapse to one `identityMismatch id
    noun path expected actual` helper.*
  - The "load snapshot → parse view → Error⇒malformed / Ok+WorkId mismatch⇒
    identityMismatch / Ok⇒ok" block is copy-pasted at `HandlersVerify.fs:399-412`,
    `HandlersShip.fs:250-263` & `:265-277`, `HandlersEvidence.fs:70-87`,
    `ViewGeneration.fs:635-636`. *Extract a generic
    `existingViewIdentityDiagnostic parse malformed mismatch path model`
    combinator.*

- **[Low] Post-kill `WaitForExit()` is unbounded.** On timeout, `proc.Kill true`
  is wrapped `try … with _ -> ()` (`CommandEffects.fs:262-265`) and immediately
  followed by an un-timed `proc.WaitForExit()` (`:267`). If `Kill` throws and is
  swallowed (unkillable process / permission fault), the bare `WaitForExit()`
  blocks forever — the exact hang the timeout exists to prevent. Vanishingly
  rare. *Give the reap a bounded wait and report the synthesized timeout result
  regardless.*

- **[Low] `answerKindValue` still prose-sniffs deferral state.**
  `EarlyStageAuthoring.fs:849-857` classifies clarification answers by
  `lowered.Contains("defer")` / `Contains("still open")`, which would misfire on
  "cannot defer" / "no longer still open". Lower blast radius than the lint case
  but the same fragility class. *Prefer word-boundary matching or an explicit
  decision tag.*

- **[Low] `ArtifactWriteKind.AuthoredSource` is modeled but never constructed**
  (`CommandTypes.fs:67,872`) — appears in no `WriteFile(...)`; its refusal path
  (`CommandEffects.fs:107`) is never exercised. Defensible contract surface
  ("the tool never writes authored prose") but indistinguishable from dead code
  to a reader. *Annotate the case, or delete and rely on `StructuredSource`/
  `AgentGuidanceTarget` (both constructed and refused).*

- **[Low] Partial functions in pure handler code** (`HandlersEvidence.fs:514,572`,
  `ValidationRunner.fs:942`) — invariant guards reachable only on a broken
  internal invariant; the top-level backstop (`Program.fs:283-304`) catches any
  escape and reclassifies as `unhandledException` (exit 2). Do not leak.
  *Optional hardening: return a typed `toolDefect` diagnostic instead of
  throwing.*

- **[Low, documented] `lint` reuses exit 2 for `UnusableInput`** (`Program.fs:427-434`),
  colliding with the tool-defect class used everywhere else
  (`ReportAssembly.fs:231-240`). Deliberate and documented, but the doctrine
  "exit 2 = tool defect" is no longer globally true. *Add a one-line caveat to
  the exit-code doc.*

### Strengths worth recording

- **Exit-code discipline is typed, not string-matched** — `exitCodeForReport`
  (`ReportAssembly.fs:231-240`) escalates to 2 only on a diagnostic's
  `IsToolDefect` bit, set at construction; a new defect escalates without a
  second registration.
- **Atomic, mode-preserving writes** with no-op skip on identical content
  (`CommandEffects.fs:46-67, 378-389`).
- **Two independent exception backstops** (`Program.fs:283-304`) prevent any raw
  CLR stack trace from leaking, including on the recovery path.
- **Single canonical run loop** `driveToReport` (`CommandEffects.fs:435-460`)
  shared by CLI and validation harness.
- **Confirm prompt routed to stderr** (`CommandEffects.fs:329`) so it can't
  corrupt the stdout JSON contract; EOF returns declined, never hangs.

---

## 4. Tests, CI, build & docs

**`dotnet test FS.GG.SDD.sln -c Release`: 1,763 passed / 0 failed / 4 skipped**
(~2m40s). The 4 skips are the network-gated composition-acceptance facts
(self-skip when `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset — correct offline
behavior); the other 36 acceptance tests are offline facts that run.

| Project | Passed | Skipped |
|---|---|---|
| FS.GG.Contracts.Tests | 106 | 0 |
| FS.GG.SDD.Artifacts.Tests | 419 | 0 |
| FS.GG.SDD.Validation.Tests | 26 | 0 |
| FS.GG.SDD.Acceptance.Tests | 36 | 4 |
| FS.GG.SDD.Cli.Tests | 207 | 0 |
| FS.GG.SDD.Commands.Tests | 969 | 0 |

### Findings

- **[Resolved — was the prior top finding] The per-PR gate now runs the full
  suite.** `gate.yml:72-73` runs `dotnet test FS.GG.SDD.sln -c Debug
  --no-build` on every push/PR to `main`; all six test projects (including
  `tier=slow` subprocess tests) block a red PR. The gate job plus
  `build-config-drift` and `api-compatibility-gate` are required status checks
  with `enforce_admins=true`.

- **[Medium] The gate uses the exact solution-wide `dotnet test` form
  DEVELOPING.md warns against.** `gate.yml:73` runs `dotnet test
  FS.GG.SDD.sln`; `DEVELOPING.md:71-77` explains `scripts/test.sh` deliberately
  loops the six projects instead because the solution-wide form hits resource
  exhaustion when every test host starts at once (`Failed to create CoreCLR,
  HRESULT: 0x80070008`, or a 90s protocol-negotiation timeout — both misread as
  a red suite). Passed cleanly here, so the hazard is environmental, but it's a
  latent flakiness risk on a constrained runner. *Either loop projects in the
  gate (as `scripts/test.sh` does) or reconcile DEVELOPING.md's warning with the
  gate choice.*

- **[Medium] DEVELOPING.md test counts are stale by ~36%.** `DEVELOPING.md:58`
  says "~1,297" total (fast=1,132 / component=1,189 at `:56-57`); actual is
  **1,767**. A contributor judging whether their local tier matched CI would be
  misinformed. The tiering mechanism itself is sound — only the numbers drifted.

- **[Low] README omits `surface` (and `lint`/`registry`/`version`).** `surface`
  is a shipped verb (`CommandHelp.fs:55-56`, `CommandTypes.fs:834`) documented
  in CLAUDE.md and `docs/reference/`, but README's cross-cutting list
  (`README.md:82-95`) never mentions it.

- **[Low] Golden-snapshot coverage is narrow.** Two golden trees
  (`Commands.Tests/goldens/{full-shape,readiness}`, 9 files) + one Artifacts
  baseline. The bulk of determinism confidence rests on the reflection
  `SurfaceBaselineTests` and property tests rather than byte-golden projections
  of each command × `--json`/`--text` state. `fsgg-sdd validate` covers the
  determinism/degradation matrix at runtime, which mitigates this. *Consider a
  few more golden snapshots of representative command outputs.*

### Strengths

- **Build hardening is strong:** `TreatWarningsAsErrors=true` (zero carve-outs),
  `Nullable=enable`, `ContinuousIntegrationBuild=true`, `Deterministic=true`;
  CI restore is locked-mode with NU1603/NU1608 promoted to errors; versions
  centrally pinned under CPM.
- **CI is mature:** api-compatibility gate (fail-closed on Indeterminate),
  build-config-drift, contract-coherence, advisory format, lockfile-sync,
  network-gated composition-acceptance; workflow comments document *why* each
  cache decision was made.
- **Test infrastructure is well-engineered, not hazardous:** pid-tagged
  per-run temp roots with self-healing dead-root sweep and per-child deletion
  (`tests/Shared/TestShared.fs:41-118`); deadlock-safe child-process runner.
- **Property tests are genuine:** FsCheck round-trip data-loss regression locks
  (`EvidenceRoundTripPropertyTests.fs`, `TasksRoundTripPropertyTests.fs`,
  `MarkdownArtifactRoundTripPropertyTests.fs`); no empty/tautological
  assertions found.
- **Public-surface baselines** enforced per test project via reflection with an
  `FSGG_UPDATE_BASELINE` update path (`SurfaceBaselineTests.fs`).

---

## 5. Roadmap

Ordered by leverage. Checkbox each as completed. Items are scoped to be
individually shippable through the SDD lifecycle.

### Correctness (do first)

- [x] **Defend against uncatchable YAML StackOverflow (§2.1).** ✅ *Done
      2026-07-15.* A pre-scan now runs in `parseYamlDocument` (`Internal.fs`)
      *before* `stream.Load`: a byte budget (`maxYamlChars = 2 MB`) bounds the
      quadratic indentation vector, and a nesting-depth limit
      (`maxNestingDepth = 100`) bounds **both** linear vectors — flow indicators
      `[`/`{` and compact block-sequence indicators (`- - - …`, which also
      overflow at ~2 bytes/level and a flow-only scan would miss). The scanner
      skips indicators inside quoted scalars/comments and resets the per-line
      dash count so a flat list never accrues depth. Over-limit input yields the
      positioned `YamlMalformed` diagnostic (exit 1). Regression tests in
      `AuthoredInputHardeningTests.fs` cover the `[[[[…` bomb, the `- - - …`
      bomb, the over-sized case, a long flat list, and a normally-nested doc;
      both bombs verified through the real CLI (exit 1, no SIGABRT).
- [x] **Bound the post-kill reap (§3, Low).** ✅ *Done 2026-07-15.* The timeout
      branch in `runProcess` (`CommandEffects.fs`) now bounds every step of the
      reap by `postKillReapMs` (5 s): the post-kill `proc.WaitForExit` takes the
      bound, and each stdout/stderr drain task is reaped via `Task.Wait
      postKillReapMs` — a task that has not completed yields `("", false)` — so a
      swallowed `Kill` throw or a grandchild that escaped the tree-kill and still
      holds the pipes can no longer relocate the hang past the timeout. The
      fail-closed exit-124 timeout result is reported regardless of `Kill`
      outcome. Regression test in `ScaffoldCommandTests.fs` drives the real
      `RunProcess` edge with a reparented grandchild holding both pipes and
      asserts a bounded return (observed ~10 s vs the ~30 s unbounded wait).
- [x] **Replace `lint` prose-matching with a structured discriminator (§3,
      Medium).** ✅ *Done 2026-07-15.* `Diagnostic` gained a parser-owned,
      non-serialized `DefectTag: string option` sub-classifier (`Diagnostics.fs`,
      stamped via `withDefectTag`; stable tag constants in the `DefectTags`
      module — `FrontMatterIncomplete`, `CoverageStableId`). The four md parsers
      stamp the front-matter defect and Specification stamps the FR/AC
      missing-stable-id defect; `LintEngine.classify` now keys on id + `DefectTag`
      instead of substring-matching parser English across the assembly boundary.
      Like `IsToolDefect`, the tag is not serialized — lint always classifies
      freshly-built diagnostics. `classify` is exposed for a regression test that
      proves a reworded message keeps its class and an untagged
      `workModelInconsistent` is no longer classified by its prose
      (`LintTests.fs`). Verified through the real CLI: `spec.md` → `coverageLine`,
      `checklist-frontmatter.md` → `frontMatter`.
- [x] **Harden `answerKindValue` deferral classification (§3, Low).** ✅ *Done
      2026-07-15.* `answerKindValue` (`EarlyStageAuthoring.fs`) no longer
      substring-sniffs: it matches each state word/phrase on a `\b` word boundary
      (so `still open` is not found inside `distill opens`), mirroring the
      Artifacts `answerKind` parser's discipline, and skips a state word that is
      **directly negated** by an adjacent negator (`cannot`/`no longer`/`not`/
      `never`/`won't`/`will not`), so the reported misfires `cannot defer` and
      `no longer still open` now read as a plain `decision`. `defer` still matches
      as a stem (defer/deferral/deferred). A non-adjacent negator (`not sure, will
      defer`) does not suppress the later keyword. Regression tests in
      `AnswerKindClassificationTests.fs` (the classifier is
      `InternalsVisibleTo`-reachable) pin the whole-word, negation, stem, and
      embedded-substring cases.

### Maintainability

- [x] **Collapse the 11 `*IdentityMismatch` constructors (§3, Medium)** into one
      `identityMismatch id noun path expected actual` helper in
      `DiagnosticConstructors.fs`. ✅ *Done 2026-07-15.* A single
      `identityMismatch id noun correction path expectedWorkId actualWorkId`
      family-shape helper now owns the shared message wording
      (`"{noun} work id '…' does not match selected work id '…'."`) and the
      `[ expectedWorkId; actualWorkId ]` relatedIds order; all 11 constructors
      (charter/specification/clarification/checklist/plan/tasks/analysis/evidence/
      verify/ship/agents-work-model) reduce to a per-artifact `(id, noun,
      correction)` triple, so a reworded message or a reordered relatedIds pair
      can no longer drift between them. Output stays byte-identical — the full
      suite (Commands/Cli golden + projection tests) passes unchanged.
- [x] **Extract the view-load/parse/identity combinator (§3, Medium)** shared by
      `HandlersVerify`/`HandlersShip`/`HandlersEvidence`/`ViewGeneration`
      (~5 copies). ✅ *Done 2026-07-15.* A single generic
      `existingViewIdentityDiagnostic parse workIdOf malformed mismatch path workId
      model` combinator in `Foundation.fs` now owns the "load snapshot → parse view →
      Error⇒malformed / Ok+WorkId mismatch⇒identityMismatch / Ok⇒none" skeleton; the
      three byte-identical `existing{Verify,Ship,Analysis}Diagnostic` functions each
      reduce to one call passing the artifact-specific parser, `WorkId` accessor, and
      the two diagnostic constructors, so a reworded shape can no longer drift between
      the stages. The two divergent prerequisite variants
      (`shipVerificationPrerequisite`, `analysisPrerequisiteDiagnosticsSummaryAndText`)
      are deliberately left as-is — they return diagnostic *lists* plus extra
      not-ready/summary payloads, so folding them would add callbacks without removing
      duplication. Output stays byte-identical — the full Commands (986) and Cli (207)
      suites pass unchanged.
- [x] **Resolve `ArtifactWriteKind.AuthoredSource` (§3, Low)** — annotate it as
      the never-constructed strict class, or delete it. ✅ *Done 2026-07-15.*
      Annotated, not deleted: the case is a genuine contract surface — the refused
      tag for a strictly authored path (`contracts/…`) the tool reads but never
      writes (`docs/reference/artifact-taxonomy.md:43`), and the
      `no command plans a WriteFile tagged AuthoredSource` test already pins the
      never-constructed invariant. Added a case-level doc comment on
      `| AuthoredSource` in both `CommandTypes.fsi` and `CommandTypes.fs` stating
      it is deliberately never constructed (naming the pinning test) and *not* dead
      code, so a reader at the DU no longer mistakes it for one. Comment-only —
      no behavior, JSON, or public-surface change; the full Commands suite passes
      unchanged.
- [x] **Convert the invariant-guard `failwith`s (§3, Low)** in
      `HandlersEvidence.fs`/`ValidationRunner.fs` to typed `toolDefect`
      diagnostics (optional hardening). ✅ *Done 2026-07-15.* Both
      `HandlersEvidence.fs` guards — the per-obligation evidence `ArtifactRef.create`
      (§3 `:514`) and the fresh-skeleton `createWorkId` (§3 `:572`) — now flow through
      the pure Commands diagnostic channel: `mergeEvidenceArtifacts` returns
      `EvidenceArtifact option * Diagnostic list`, validating the work id and evidence
      `ArtifactRef` **once** (the ref is threaded into every skeleton declaration rather
      than re-created per obligation), and on the unreachable rejection returns `None`
      plus a `DiagnosticConstructors.toolDefect` (exit 2 via `IsToolDefect`); the caller
      surfaces the diagnostic and produces no evidence. `workIdDiagnostics`
      (`Foundation.fs:756`) already validates the work id at plan time, so the guard
      cannot fire in production — the tests bypass planning and call
      `mergeEvidenceArtifacts` directly, pinning both the seeded-skeleton happy path and
      the `toolDefect`-not-crash arm (`EvidenceCommandTests.fs`). The
      `ValidationRunner.fs:942` site is left a documented hard invariant: `run` returns
      the released, golden-tested `ValidationReport` contract, which has no top-level
      diagnostic channel, and the guard is a pure-fold postcondition of
      `ValidationMsg.BuildReport` that cannot fire; threading a diagnostic would mutate
      the released schema for an unreachable case, so it stays an `invalidOp` assertion
      (caught by the `Program.fs` backstop as `unhandledException`, the same fail-closed
      exit 2 a `toolDefect` yields), now with a comment explaining the deliberate choice.
      Output stays byte-identical — the full Commands (988), Cli (207), and Validation
      (26) suites pass.
- [ ] **Pick one `.fsi` convention for internal submodules (§1, Low)** and
      document it (drop the 3 stray `.fsi` or add the rest).
- [ ] **Scope `System.IO` out of the pure core (§1, Low)** — `open type
      System.IO.Path` / `Path` alias in the workflow modules.
- [ ] **Split the multi-stage authoring modules (§1/§3, Low)** —
      `ChecklistPlanAuthoring.fs` (two stages) and `EarlyStageAuthoring.fs`
      (three), if either grows further.

### CI, build & docs

- [ ] **Reconcile the gate `dotnet test` form with DEVELOPING.md (§4, Medium)** —
      either loop the six projects in `gate.yml` (as `scripts/test.sh` does) or
      update DEVELOPING.md's warning to note the gate's deliberate choice.
- [ ] **Refresh DEVELOPING.md test counts (§4, Medium)** to ~1,767 (and the
      per-tier numbers).
- [ ] **Add `surface`/`lint`/`registry`/`version` to the README command list
      (§4, Low).**
- [ ] **Add a few golden snapshots (§4, Low)** for representative command ×
      `--json`/`--text` outputs to widen the determinism net.

### Optional / defensive

- [ ] **Widen the YAML catch to a defensive superclass (§2, Low)** so a future
      non-`YamlException` construct degrades to a diagnostic rather than the
      §2.1 crash outcome.
- [ ] **Add a one-line exit-code caveat (§3, Low)** noting `lint`'s bespoke
      `UnusableInput`=2 polarity.

---

*Generated 2026-07-15 11:06:24 CEST against `main` @ `6fe63df`. Four parallel
review passes; crash-class findings verified empirically against the built
assemblies. No `src/` files were modified.*
