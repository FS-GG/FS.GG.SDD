---
title: Code quality and architecture review
category: Reports
date: 2026-07-02
generated: 2026-07-02 14:06:16 CEST
scope: full repo — src/, tests/, .github/, scripts/, docs/, build props
---

# Code Quality & Architecture Review: FS.GG.SDD

A thorough, evidence-based review of the repository at `main` (`55e80d5`,
v0.5.0, .NET SDK 10.0.301) covering architecture and layering, correctness
risks in the Artifacts/Contracts layer, the command/CLI layer, the test
suite, and build/CI/docs hygiene. Every claim is cited `File.fs:line` and was
checked against source on 2026-07-02; correctness findings marked
**[verified]** were reproduced empirically (FSI probes against the built
assemblies).

This is an analysis, not a change. Nothing in `src/` was modified. Baseline
today: clean build with **2 unique warning sites** (both FS3262, benign),
**835/835 tests pass** (3 skipped — the network-gated acceptance facts, as
designed).

## Method

- Five parallel review passes: architecture/layering, Artifacts+Contracts
  code quality, Commands/Cli/Validation code quality, test-suite quality,
  and build/CI/docs hygiene — each with full file reads and targeted greps.
- Baseline: `dotnet build --no-incremental` (warning census) and
  `dotnet test` (full suite) on this machine.
- Empirical verification of the highest-risk parser findings via FSI against
  the built `FS.GG.SDD.Artifacts`/`FS.GG.Contracts` assemblies.

## Scorecard

| Metric | Value |
|---|---|
| `src` F# LOC | 22,476 across 86 `.fs` files (5 projects) |
| `tests` F# LOC | 16,663 across 125 files (6 projects) |
| Largest source file | `CommandReports.fs` — 1,555 lines |
| Unique compiler warnings (clean rebuild) | **2** (both FS3262) — down from ~290 in the 2026-06-26 report |
| Test results | 835 passed / 0 failed / 3 skipped (gated) |
| Per-PR CI test execution | **none** — gate.yml builds but never runs `dotnet test` |
| Verified crash classes in authored-input parsing | 3 (YamlException, OverflowException, keyword misclassification) |
| Project dependency graph | strictly acyclic, `.fsi` on every public module |

## Executive summary

The architecture is in genuinely good shape — better than most codebases of
this size. The MVU pure-core/effect-edge boundary is real and mechanically
verifiable (zero IO/clock/env calls in the Commands core outside
`CommandEffects.fs`), layering is strictly acyclic with Spectre.Console
quarantined in the CLI host, determinism is engineered end-to-end, and the
June refactor report's headline god-module finding (6,838-line
`CommandWorkflow.fs`) has been fully addressed — the largest file is now
1,555 lines and the warning count fell from ~290 to 2.

The highest-leverage problems found now are:

1. **The per-PR CI gate never runs tests** (`.github/workflows/gate.yml`) —
   only build, config-drift, and ApiCompat. A PR can merge with most of the
   835-test suite red; only two of seven test projects run in CI at all, and
   only on release events.
2. **Hand-authored YAML can crash the CLI** instead of producing a
   diagnostic — `Internal.parseYaml` doesn't catch `YamlException`
   **[verified]**, and `SchemaVersion.parse` can throw `OverflowException`
   **[verified]**. These violate the product's own malformed-input-is-exit-1
   doctrine.
3. **Prose keyword sniffing produces wrong lifecycle states** — a
   clarification line marked `(unanswered)` classifies as *answered*
   **[verified]** because the check is `Contains("answered")`.
4. **`RunProcess` has no timeout** — every external process edge (scaffold's
   `dotnet new`, upgrade's `dotnet tool update`, git) can hang the CLI
   forever.

Everything else is maintainability: quantified duplication in the command
layer, dead contract surface (`OverwritePolicy`, unused effects), stale
docs, and test-infrastructure hazards.

---

## 1. Architecture & layering

### 1.1 Layering — holds (strength)

Verified from `.fsproj` ProjectReferences:

```
FS.GG.Contracts        (FSharp.Core only — BCL-only leaf)
   ↑
FS.GG.SDD.Artifacts    (Contracts + YamlDotNet)
   ↑
FS.GG.SDD.Commands     (Contracts + Artifacts)
   ↑
FS.GG.SDD.Validation   (Artifacts + Commands — no Spectre, no Governance)
   ↑
FS.GG.SDD.Cli          (all four + Spectre.Console — the only Spectre ref)
```

Strictly acyclic; every public module has a paired `.fsi`; only the
`internal` `CommandWorkflow/` modules lack them (consistent). The CLAUDE.md
boundary claims were audited and **hold**: no provider/rendering-specific
token in generic SDD, seeded-tree ownership guards match the doc
(`HandlersScaffold.fs:53-68`), `nextLifecycleCommand` returns `None` for all
cross-cutting commands (`CommandTypes.fs:605-622`), `SkillMirror` is
genuinely the single mirror authority shared by init/scaffold/refresh/drift,
and doctor plans reads only.

### 1.2 The MVU pure core is actually pure (strength)

A grep for `File.`, `Directory.`, `Console.`, `Process`, `DateTime`,
`Environment.`, `Guid.NewGuid` across `FS.GG.SDD.Commands` finds **zero**
hits outside `CommandEffects.fs`. Handlers compute exclusively over
`FileSnapshot`s from interpreted read effects; the single edge interpreter
is `CommandEffects.interpret` (`CommandEffects.fs:197-271`); staged drivers
(scaffold's mirror/git/chmod phases, upgrade's confirm walk) re-derive their
phase purely from the interpreted-effect log. Dry-run, replay, and the
Confirm re-derivation all fall out of one interpreter. This is rare and
worth protecting.

### 1.3 Dead contract surface (medium)

- **`OverwritePolicy` is dead plumbing.** Set per-command
  (`Program.fs:194`), serialized into every report
  (`CommandSerialization.fs:550`, `CommandReports.fs:1480`), and consulted
  by **nothing** — `canOverwrite` (`CommandEffects.fs:42-48`) decides purely
  on `ArtifactWriteKind`. The JSON contract advertises a knob with no
  behavior. Note also that `canOverwrite` unconditionally allows overwriting
  `AuthoredSource` at the edge; no-clobber semantics live entirely in the
  planners, so the interpreter is not a real last-line guard.
- **Dead effect vocabulary.** `EmitStdout`/`EmitStderr`/`SetExitCode` are
  defined (`CommandTypes.fs:455-457`) and interpreted
  (`CommandEffects.fs:260-268`, including a process-global
  `Environment.ExitCode` mutation that competes with `main`'s return value)
  but no handler ever plans them. Delete or document.
- **Vestigial messages.** `CommandMsg`'s `LoadProject | LoadWorkItem |
  ApplyUserIntent | PlanGeneratedViewRefresh` are no-ops
  (`CommandWorkflow.fs:192-195`).

### 1.4 Module organization (medium)

- **`CommandReports.fs` (1,555 lines) is the new god module**: ~90
  diagnostic constructors (:19-918), the effect→artifact-change projection
  (:918), four correction-command policies (:999-1119), a ~300-line
  `nextAction` cascade (:1128-1421), report assembly (:1441), and exit-code
  policy (:1549). Three separable responsibilities — diagnostic catalog,
  next-action routing policy, report assembly.
- **One flat `[<AutoOpen>]` internal namespace**: every `CommandWorkflow/`
  file is `[<AutoOpen>] module internal` in `FS.GG.SDD.Commands.Internal`,
  so hundreds of functions share one flat scope and inter-file dependencies
  exist only as fsproj compile order. Call-site provenance is invisible.
- **Compile-order file names**: `ParsingEarly`/`ParsingMid`/`ParsingTasks`
  (1,102 / 1,013 / 745 lines) are named for position, not responsibility.
- **`nextLifecycleEffects` threads a 12-tuple** of summary options through
  every command arm (`CommandWorkflow.fs:34-65`); adding a stage means
  editing every arm. A partial-summaries record would collapse this.

### 1.5 Purity soft spots (low)

- `RegistryDocument.load` does real filesystem IO inside the Artifacts
  library (`LifecycleArtifacts/RegistryDocument.fs:93-100`) — the one
  artifact-load edge outside the host.
- `ValidationRunner.fs:420-428` mutates `TZ` process-wide (thread-hostile,
  deliberate for the perturbation matrix).
- `Foundation.projectIdFromRoot` (`Foundation.fs:31`) resolves `"."` against
  ambient cwd inside a pure planner.
- `SeededSkills.seededSkills` (`SeededSkills.fs:57-58`) `failwithf`s at
  static init — a missing embedded resource surfaces as an opaque
  `TypeInitializationException`.
- `Program.fs:117` (`validate --out`) is the one product file-write that
  bypasses the `WriteFile`/no-clobber machinery — and it is unguarded (§3.3).

---

## 2. Correctness risks — Artifacts & Contracts

The three high findings below are **empirically reproduced**, not
speculative. All of them turn hand-authored artifact input into crashes or
wrong lifecycle states, violating the malformed-input→diagnostic→exit-1
doctrine.

### 2.1 HIGH — malformed YAML throws through Result-returning parsers [verified]

`Internal.parseYaml` (`LifecycleArtifacts/Internal.fs:26-34`) calls
`YamlStream.Load` with no try/catch. YamlDotNet throws on syntactically
invalid YAML — reproduced with a tab-indented `.fsgg/project.yml`
(`SyntaxErrorException`) and a duplicate `id:` key in `tasks.yml`
(`YamlException: Duplicate key`). Every YAML lifecycle parser returns
`Result<_, Diagnostic list>` yet leaks these exceptions:
`Config.parseProjectConfig`/`parseSddLifecyclePolicy`/
`parseAgentGuidanceConfig`/`parseProviderRegistry`
(`Config.fs:49,84,110,154`), `Task.parseTaskFacts` (`Task.fs:181`),
`Evidence.parseEvidenceArtifact` (`Evidence.fs:176`), and the front-matter
parsers (`Specification.fs:61`, `Clarification.fs:102`, `Checklist.fs:80`,
`Plan.fs:113`, `WorkItemMetadata.fs:31`). The exception propagates through
`WorkItem.loadWorkItemFromSnapshots` (`WorkItem.fs:114-145`) into
`Serialization.generateWorkModel`. Tellingly, `WorkItem.rawSchemaVersion`
*does* wrap its own `parseYaml` in `try…with _ -> None`
(`WorkItem.fs:54-59`) — the hazard is known but defended at one of ~12
sites. **Fix: catch `YamlException` inside `Internal.parseYaml`** — a
one-line change that removes the whole crash class.

### 2.2 HIGH — keyword sniffing misclassifies lifecycle state [verified]

`Clarification.parseClarificationQuestions` (`Clarification.fs:192`) uses
`lowered.Contains("answered")` — so `- CQ-001: … (unanswered)` parses as
state **answered** (reproduced). Clarify-stage readiness derives from these
states. The same substring-sniffing pattern recurs:

- `Checklist.parseChecklistResultsInSection` (`Checklist.fs:180-187`):
  `Contains("fail")` matches "failsafe"; "pass (advisory note attached)"
  classifies as `advisory`.
- `Clarification.answerKind` (`Clarification.fs:198-204`): `Contains("note")`
  matches "noted".
- `Clarification.fs:190`: `Blocking = not (Contains "non-blocking")` misses
  "nonblocking".
- `Plan.planDecisionStatus` (`Plan.fs:215-223`), contract-kind and
  evidence-kind sniffing (`Plan.fs:246-251`, `Plan.fs:267-273`).
- Related prose sentinels in the command layer: blocking findings are
  filtered with `StartsWith("No ")` (`ParsingMid.fs:494,1003`) — a real
  finding "No tests cover FR-003" is silently treated as the placeholder and
  does not block; `isNoOutstandingSentinel` prefix-matches `"None"`
  (`Internal.fs:216-221`), exempting "Nonexistent flag behavior is unclear".

**Fix: word-boundary regexes or an explicit `[state: …]` token grammar**,
matching the discipline the id patterns already use.

### 2.3 HIGH — `SchemaVersion.parse` overflows [verified]

`SchemaVersion.fs:30-43`: the regex admits any digit run, then
`Int32.Parse` throws `OverflowException` on `schemaVersion:
99999999999999999999`. Since `classifyRaw` feeds this from authored
artifacts, one absurd-but-typeable scalar crashes normalization instead of
yielding `Malformed`. Use `Int32.TryParse` with
`NumberStyles.None`/invariant culture — which also fixes:

### 2.4 MEDIUM — loose, culture-sensitive version parsing [verified]

`Fsgg.Version.tryParse` (`Contracts/Version.fs:10-17`) accepts `"1. 2.+3"`
as `1.2.3` (reproduced), plus `" 1.2.3 "`, `"01.02.03"`, `"+1.+2.+3"` —
and bare `Int32.TryParse(string)` uses current culture. This grammar gates
provider `minimumCliVersion` coherence (`Diagnostics.fs:273-282`) and
registry SemVer checks. Same looseness at `Internal.fs:275`,
`RegistryDocument.fs:111`, `Checklist.fs:144`, `Plan.fs:200`, `Task.fs:131`,
`Evidence.fs:133`.

### 2.5 MEDIUM — silent drops, bypassed smart constructors, split policies

- **Malformed references vanish silently** (M3):
  `Internal.parseTaskIds/parseRequirementIds/…` (`Internal.fs:234-244`) use
  `Result.toOption`, so a typo'd `dependencies: [T01]` disappears — no
  diagnostic, and `WorkModel.referenceDiagnostics` never sees the edge. A
  silently-dropped dependency can flip verify readiness. Task/evidence
  entries with unparseable ids are likewise skipped wholesale
  (`Task.fs:198-199`, `Evidence.fs:192-193`); incomplete provider entries
  drop with no info diagnostic (`Config.fs:159-162`).
- **Id smart constructors are advisory**: the `.fsi` exposes transparent
  records (`Identifiers.fsi:4-33`), and internal fallback paths construct
  unvalidated values (`Task.fs:104`, `WorkItem.fs:79`). Either make the
  representation private or drop the Result ceremony — the halfway state
  gives false assurance.
- **Three version-support policies disagree**: `SchemaVersion.classifyRaw`
  (canonical: 1=Current, 0=Deprecated, 2=Unsupported, >2=Future) vs
  `WorkModel.parseWorkModel` accepting `version >= 1` (`WorkModel.fs:579` —
  a schemaVersion-3 work model parses here, blocks everywhere else) vs
  `SchemaVersion.isSupported` (Major-only check) used by
  `ScaffoldProvenance.tryParse` (`ScaffoldProvenance.fs:126-127`).
- **Digest normalization differs between projects** (M5):
  `SchemaVersion.sha256Text` normalizes CRLF→LF before hashing
  (`SchemaVersion.fs:124-127`); `Fsgg.SkillMirror.sha256` hashes raw bytes
  (`SkillMirror.fs:12-16`). A CRLF checkout can hash-mismatch a manifest
  digest for logically identical content — directly relevant to the 057/058
  per-skill sha256 contract.
- **Regex surgery on serialized JSON for the self-referential digest**:
  `Serialization.canonicalizeOutputDigestForHash` (`Serialization.fs:174-179`)
  regex-nulls the `outputDigest` object; correct only while the writer's
  formatting is frozen. A structural serialize-with-`OutputDigest = None`
  approach removes the fragility.
- **Mixed error idioms** (M7): `Result<_, Diagnostic list>` at the edges,
  but `invalidArg`/`failwithf` internally at ~15 sites (e.g.
  `Serialization.fs:193-195` feeds *parsed source paths* into `invalidArg`),
  while `ScaffoldProvenance.tryParse` returns `option` via a catch-all and
  `ReleaseContract.parse` returns `Result<_, string>`.

### 2.6 Low

Copy-paste `*IdsInLine` sextets (`Clarification.fs:131-171`,
`Plan.fs:146-179`, ~90 lines duplicating an unused `Internal.idsInLine`);
dead `elif` in `planDecisionStatus` (`Plan.fs:222-223`);
`Registry.compareSemVer` duplicates `Version.compareParsed` under a comment
promising delegation (`Registry.fs:77-83`); template-residue unused opens
across `LifecycleArtifacts/*.fs`; version literals scattered
(`ContractVersion.fs:5-9` vs fsproj, `SchemaVersion.fs:162` vs
`ReleaseContract.fs:182`); `Map.ofList` last-wins on duplicate paths at five
sites; sequence indexes recorded as line numbers in diagnostics
(`Task.fs:140,219`, `Evidence.fs:136,156,230`).

Path handling and serialization ordering were audited and are **clean**:
`\→/` normalization at every ingestion point, explicit sorts before every
serialized list, no `Map`/`Set` iteration feeding writers, no clock usage.

---

## 3. Command layer — Commands / Cli / Validation

### 3.1 HIGH — `RunProcess` has no timeout

`proc.WaitForExit()` (`CommandEffects.fs:131`) blocks forever; there is no
kill/cancellation path. Every process edge — scaffold's `dotnet new
install/update/create` (`HandlersScaffold.fs:205-233`), upgrade's `dotnet
tool update` (`HandlersUpgrade.fs:22-23`), the git probe/init — can hang the
CLI indefinitely on a wedged network or child process. Relatedly, the 64 KiB
output cap bounds *retention*, not memory: `ReadToEndAsync()` fully
materializes both streams before truncation (`CommandEffects.fs:129-133`) —
a pathological provider can balloon the process first. (The deadlock-safe
concurrent stream reads and non-throwing UTF-8 decode are done correctly.)

### 3.2 HIGH — defect escalation rides a hand-maintained string set

`providerDefectIds` (`CommandReports.fs:1536-1547`) is the only thing that
promotes exit 1 → exit 2; nothing types "this diagnostic is a defect", so a
forgotten entry silently demotes a defect. The consumer-side literal problem
already produced a phantom: `verifyCorrectionCommand` routes on
`"unsupportedTaskStatus"` (`CommandReports.fs:1087`), an id produced nowhere
(grep-confirmed). Substring matching on ids (`diagnostic.Id.IndexOf("stale")`,
`HandlersAgents.fs:237`) will misfire on any future id containing "stale".
Diagnostic *producers* are exemplary (one constructor per id with
message+correction); consumers need the same central constants — ideally an
`IsDefect` property on the diagnostic itself.

### 3.3 MEDIUM — edge and contract nicks

- **`Confirm` prompt writes to stdout** (`CommandEffects.fs:182-183`), gated
  on stdin interactivity only (`Program.fs:203`). With TTY stdin and
  redirected stdout (`fsgg-sdd upgrade > out.json`), prompt bytes prepend
  the deterministic JSON and the user never sees the question. Prompt
  belongs on stderr.
- **Blocked reports route to stderr but capabilities are sensed on stdout**
  (`Program.fs:234` vs `Rendering.fs:32,42`): with stdout a TTY and stderr
  redirected (`--rich 2>err.log`), ANSI escapes land in the file —
  violating the degrade-on-redirect contract.
- **`validate --out` and the harness run are unguarded**: `File.WriteAllText`
  (`Program.fs:117`) throws a raw stack trace on a bad path — the product's
  own user-input-vs-defect taxonomy is bypassed.
- **Dead staleness check**: `ParsingTasks.fs:715-719` —
  `if taskSourceSnapshotStale … then [] else []`, both branches empty,
  called with empty source texts. The tasks prerequisite never re-checks
  snapshot digests against live sources; a stale tasks graph can flow into
  verify/ship gated only by a non-blocking warning (`ViewGeneration.fs:445-481`).
- **Magic-comment switches with fabricated ids**: production parsing reacts
  to sentinel HTML comments and reports hardcoded ids
  (`unsafe-result-change` → `"CR-001"`, `ParsingMid.fs:394-395`;
  `unsafe-decision-change` → `"PD-001"`, `ParsingMid.fs:904-905`;
  `unsafe-status-change` → `"T001"`, `ParsingTasks.fs:607-608`). Test hooks
  living in the product, reporting invented "changed" ids.
- **Arg parsing accepts flags as values and ignores unknown flags silently**:
  `verify --work --json` yields `WorkId = Some "--json"`; `--wrok x` drops
  with no diagnostic (`Program.fs:13-41`). A trailing `--param` with no
  value is silently dropped. Help metadata (`CommandHelp.fs:48`) omits
  `--dry-run` for charter and is a disconnected table.
- **`dotnet new update` runs by default on every scaffold**
  (`HandlersScaffold.fs:211,232`), mutating global template state unless
  `--no-update`; result deliberately ignored.
- **`--text`/`--rich` drop a JSON fact**: `skillDriftPaths` — the primary
  058 content-drift surface — is serialized (`CommandSerialization.fs:394,415`)
  but never rendered in the doctor/upgrade text/rich blocks
  (`CommandRendering.fs:246-285`). A human running `doctor --text` cannot
  see which skill copies drifted.
- **Rich table is built by re-parsing text output**: `renderRichTo` splits
  `renderText` lines on the first `": "` (`Rendering.fs:94-106`) — feature
  054 already had to single-line-encode provider stdout to protect this.
  It also means the projections are not information-ordered json ⊇ rich ⊇
  text (rich adds paths/diagnostic rows text lacks; text renders
  `diagnostics: <count>` only, `CommandRendering.fs:288`). And `renderText`
  itself is a 300-line manual mirror with no completeness guarantee — each
  new summary field must be added in three places.
- **Stale user-facing strings**: `unknownCommand` lists 11 commands,
  omitting agents/refresh/scaffold/doctor/upgrade/validate/registry
  (`CommandReports.fs:44`); the `reseedSeededSkills` NextAction omits the
  056 `.agents/skills` root (`CommandReports.fs:1207`); `buildReport`
  hardcodes `ProjectRoot = "."` regardless of `--root`
  (`CommandReports.fs:1477`).

### 3.4 Duplication inventory (quantified)

The high-value seams already exist (`Prerequisites.resolvePrerequisites` +
`runHandler`, `Drift.compute`, `SkillMirror`). What remains:

- **verify/ship JSON writers**: ~150 near-identical `Utf8JsonWriter` lines
  (`HandlersVerify.fs:228-387` vs `HandlersShip.fs:214-363`), plus
  `analysisJson` (`ViewGeneration.fs:263-382`) repeating the same
  sub-writers; `verifySourceKind`/`verifySources` duplicate their analysis
  counterparts. One `writeReadinessEnvelope` would guarantee the views can't
  drift structurally.
- **Blocked-work-model fallback copy-pasted 9×**: `HandlersEarly.fs:52,89,
  128,173,220`, `HandlersAnalyze.fs:63`, `HandlersVerify.fs:461`,
  `HandlersShip.fs:411`, `HandlersEvidence.fs:609`.
- **Front-matter identity diagnostics ×10** (6–15 lines each) across
  `ParsingEarly.fs:509-514,545-551,1027-1035,1084-1092`,
  `ParsingMid.fs:400-410,458-468,910-922,965-977`,
  `ParsingTasks.fs:613-627,697-713` (~120 lines).
- **Snapshot-staleness algorithm ×3** (`ParsingMid.fs:305-315,841-852`,
  `ParsingTasks.fs:312-322`).
- **MVU drive loop ×2**: `Program.fs:207-227` duplicated verbatim as
  `ValidationRunner.runRequest` (`ValidationRunner.fs:71-94`) — divergence
  changes validate-vs-CLI behavior invisibly.
- **Rich-console construction ×3** (`Rendering.fs:301-315,331-345`,
  `RegistryValidate.fs:151-164`).
- **Ten per-stage read-effect lists differing by ~one line each**
  (`Foundation.fs:405-530`, ~130 lines); prereq consumers re-destructure the
  resolution record into ~20 positional locals at every site.
- **Correction-command policies overlap** (`CommandReports.fs:999-1044`,
  `:1073-1093` vs `:1167-1181`).

### 3.5 Complexity hotspots (worst five)

1. `computeRefreshPlan` — ~480 lines (`HandlersRefresh.fs:128-607`), nine
   responsibilities over stringly `"refreshed"/"blocked"/…` states.
2. `nextAction` — ~290 lines, a 15-branch `elif` cascade
   (`CommandReports.fs:1128-1420`).
3. `computeVerifyPlan` — ~175 lines with an 11-way `Some,…,Some` tuple match
   (`HandlersVerify.fs:412-413`), plus 160 lines of `verifyJson`.
4. `computeShipPlan` — ~175 + 150 lines (`HandlersShip.fs:365-539`).
5. `nextLifecycleEffects` — ~130 lines threading the 12-tuple
   (`CommandWorkflow.fs:21-151`).

String-typed working state is the common thread: view-currency words,
upgrade step outcomes (`"wouldApply"`, `"applied"`, …), and step ids are raw
strings compared across files (`HandlersUpgrade.fs:44-48,144-147`,
`Drift.fs:124-217`) even where DUs exist for the same concepts.

### 3.6 Validation harness

The approach is strong (real workflow over real temp filesystems, declared
plan × independently-enumerated surface, `NotValidated` for unselected cells
so partial runs can't read as full passes, exhaustive DU match forcing
compile-time coverage decisions). Gaps:

- **Temp-directory leak**: `tempDirectory()` (`ValidationRunner.fs:105-108`)
  is never cleaned — ~350 full project copies per `validate` run left in
  `%TMP%`, forever. (Tests share this: `TestSupport.tempDirectory` across
  ~800 facts has no cleanup either.)
- **The environment dimension is partly nominal**: determinism cells declare
  `ColorDisabled`/`TermDumb`/`NonInteractiveRedirected`/`Interactive`
  (`ValidationHarness.fs:56`) but `evaluateDeterminismCell`
  (`ValidationRunner.fs:312-335`) branches only on
  `PerturbedHostEnvironment` — four of five environment cells run the
  identical neutral comparison, and `NO_COLOR`/`TERM` are never actually
  set. `withPerturbedHost` varies culture+TZ but not cwd despite its
  comment.
- The library's `Rich` projection is defined as `renderText`
  (`ValidationRunner.fs:203-209`) — deliberate (no Spectre dep), but the
  matrix never exercises the real rich path, so its ANSI check can never
  fail for Rich cells.

---

## 4. Test suite

835 facts, xUnit v2 everywhere, uniform idiom (module-per-file, per-project
`TestSupport`, real filesystem + real MVU interpreter, explicit "no mocks"
doctrine). Requirement traceability in test names throughout.

### 4.1 Strengths worth naming

- **Drift guards that test against live code, not copies.**
  `EarlyStageGuidanceContractTests.fs` seeds via real `init` then resolves
  every documented command, heading, id-regex, and the §1.1/§1.2 grammar
  blocks against the live parsers — a doc/contract divergence fails
  deterministically. `SeededSkillsTests.fs:172-183` pins embedded skills to
  the on-disk authored set byte-for-byte with a 45-effect fan-out tripwire.
  These would genuinely fire.
- **Determinism by construction**: run-twice byte equality, ordered
  substring property-order pins, fixed-width injected Spectre console with
  the `GITHUB_ACTIONS` ANSI-re-enable footgun explicitly defeated — instead
  of brittle golden files.
- **Honest failure-path coverage**: exit-1 vs exit-2 taxonomy (16 explicit
  exit-2 assertions in scaffold tests alone), no-clobber refusals,
  non-interactive prompt refusal, provider-intrusion rejections. The
  acceptance gating (`RequiresRegistryFactAttribute`,
  `AcceptanceSupport.fs:63-69`) skips correctly at discovery time with a
  meta-test verifying the skip.

### 4.2 HIGH — Commands.Tests CLI smokes hardcode `-c Release --no-build`

`VerifyCommandTests.fs:38-40` and `AgentsCommandTests.fs:250` spawn
`dotnet run --project src/FS.GG.SDD.Cli -c Release --no-build`, but
`FS.GG.SDD.Commands.Tests.fsproj` has **no Cli ProjectReference**. A
Debug-only local `dotnet test` either fails or silently exercises a stale
Release binary. `Cli.Tests/ValidateCommandTests.fs:13-16` already shows the
correct pattern (config auto-detection + ProjectReference).

### 4.3 MEDIUM — environment races and orphaned fixtures

- `ScaffoldCommandTests.fs:1006-1012` swaps process-global `PATH` inside a
  collection serialized only against one other class; other collections in
  the same assembly spawn processes in parallel during the window.
  `Acceptance.Tests` nulls `FSGG_SDD_ACCEPTANCE_REGISTRY` and crushes `PATH`
  (`CompositionAcceptanceTests.fs:229-265`) with **no**
  `DisableTestParallelization` (only Validation.Tests has one) — when the
  registry *is* set (scheduled CI), parallel classes can observe the
  mutated env.
- **~106 of 107 fixture manifests are orphaned**:
  `tests/fixtures/lifecycle-commands/*/manifest.yml` declare expected
  command/outcome/changedArtifacts, but the only consumer is
  `CommandReportJsonTests.fs:12` (`deterministic-report`). They read as a
  planned manifest-driven harness superseded by hand-written facts — wire
  them into the Validation harness or delete them; today they are
  misleading documentation.
- Baseline-update workflow is inconsistent: `FSGG_UPDATE_BASELINE=1`
  self-rewrite exists in 1 of 4 `PublicSurface.baseline` tests
  (`Contracts.Tests/PublicSurfaceTests.fs:62-63`); the rest are hand-edit.
- `findRepoRoot`/`writeRelative` helpers are triplicated across
  TestSupport modules; the 9-command `initializeVerifiedProject` ladder
  injects evidence with hardcoded T001–T006 ids (silent invalidation if
  seeded task count changes); ~800 facts leak `%TMP%/fsgg-sdd-<guid>` dirs
  with no cleanup fixture.
- Coverage shape: every handler file has a direct test counterpart (scaffold
  deepest at 1,662 test lines); the thinnest ratio is the ~2.9k LOC of
  `Parsing*` exercised only indirectly through command-level tests — which
  is exactly where the §2.2 keyword-sniffing bugs live.

---

## 5. Build, CI, docs, repo hygiene

### 5.1 HIGH — the per-PR gate never runs tests

`gate.yml` has three jobs: locked restore + build (:37-48), shared-config
drift (:50-79), ApiCompat (:97-116). **No `dotnet test` anywhere in the
per-PR/push path.** Of 7 test projects, only `Contracts.Tests` and
`Cli.Tests` run in CI at all — and only on release events
(`release.yml:134-188`); `Artifacts.Tests`, `Commands.Tests`,
`Validation.Tests`, and the offline acceptance facts never run in any
workflow. A PR can merge with the bulk of the suite red. Compounding it,
`gate.yml:94-96` admits there is no branch protection, so even a red gate is
advisory. Given this repo's evidence/verification doctrine, this is the
single largest hygiene gap. Fix: add `dotnet test FS.GG.SDD.sln` (offline
suite) to `gate.yml` — and note the §4.2 Release-hardcoding issue will
surface immediately when CI runs Commands.Tests in Release.

### 5.2 MEDIUM — restore hermeticity, caching, lint

- **Non-hermetic `nuget.config`** (`nuget.config:1-6`): adds the local feed
  but deliberately doesn't `<clear/>` inherited sources (sanctioned by spec
  040), so restore depends on per-machine NuGet config — the most plausible
  root of the recorded FSharp.Core hash local↔CI divergence and the
  lockfile-regeneration pain (`3330a06`). All 10 committed lockfiles
  currently agree on FSharp.Core 10.1.301. A `<clear/>` + explicit sources +
  source mapping kills the divergence class at the root.
- **No NuGet caching in CI**: all three workflows use `setup-dotnet@v4`
  without `cache: true` despite committed lockfiles being the perfect cache
  key. Every job pays full restore.
- **No formatting/analyzer lint**: no fantomas, no `.editorconfig`, no
  format-check job. Consistency is convention-borne.
- **Warning ratchet is narrow**: `TreatWarningsAsErrors=false` with only
  `FS3261;FS0025;NU1603;NU1608` promoted
  (`Directory.Build.local.props:24,31`). Deliberate (feature 026), but the
  codebase is now at 2 warnings — cheap to widen the ratchet or flip full
  WaE.
- **Locked-restore boilerplate duplicated 5×** across gate/release
  workflows, one copy already divergent — composite-action material.

### 5.3 Docs staleness (medium) and low items

- `README.md` and `docs/quickstart.md` omit `doctor`/`upgrade` entirely;
  `docs/index.md` doesn't link `reference/doctor-upgrade.md`, and
  `docs/index.md:26-27` still says the repo "starts as an empty Spec Kit
  product scaffold" — against 756 files under `specs/` and a shipped 0.5.0.
- `DEVELOPING.md` counts four projects (Contracts missing) and locates the
  warning ratchet in the wrong props file.
- **CLAUDE.md and AGENTS.md are two hand-maintained near-copies** with no
  drift guard — unlike the skill trees, which are guard-pinned. The repo's
  own doctrine says keep the two surfaces aligned; they have already
  diverged textually from line 3 on.
- `release.yml:4-6` header comment hardcodes stale versions (1.1.0/0.2.0 vs
  actual 1.4.0/0.5.0); fallback NuGet user `'Paradigma11'` embedded at
  `release.yml:262,342` (bus-factor smell).
- `scripts/verify-cli-tool.sh` (the packed-tool self-containment smoke) is
  wired to nothing — it guards a load-bearing property only if someone
  remembers to run it.
- Orphan `fake-cli` pin in `.config/dotnet-tools.json` (org-managed; fix
  upstream). No `RollForward` on the packed tool (net10.0-only). Evidence
  placement is inconsistent (specs 016/017 use `specs/<id>/readiness/`;
  019/020/021/046 use root `readiness/<id>/`, which also
  namespace-collides with the product's own generated-output convention).
- Link health: spot-check of relative links across README/DEVELOPING/docs
  found **zero broken targets**. `assets/` is one 12 KB icon; no
  large-binary problem; Renovate is configured.

### 5.4 Strengths

- **Exemplary supply-chain/release discipline**: CPM + transitive pinning +
  committed lockfiles + CI locked-mode + org-config byte-drift gate +
  evaluated-version release resolution with tag-coherence guards + OIDC
  trusted publishing + empty-pack loud failure.
- **Self-documenting infrastructure**: every workflow job, props block, and
  script carries its FR/ADR rationale inline; CI delegates to scripts
  rather than duplicating them.
- **API-surface rigor**: `.fsi` on every library, `PublicSurface.baseline`
  snapshots, an F#-aware ApiCompat gate.

---

## 6. Prioritized remediation plan

Ordered by risk × effort. The first four are small, contained changes that
close real defect classes.

| # | Action | Where | Size |
|---|---|---|---|
| 1 | Add `dotnet test` (offline suite) to the PR gate; fix the Commands.Tests `-c Release --no-build` smokes first (they will fail in CI Release runs without a Cli ProjectReference) | `gate.yml`, `VerifyCommandTests.fs:38`, `AgentsCommandTests.fs:250` | S |
| 2 | Catch `YamlException` in `Internal.parseYaml`; `Int32.TryParse` + invariant/`NumberStyles.None` in `SchemaVersion.parse` and `Fsgg.Version.tryParse` | `Internal.fs:26`, `SchemaVersion.fs:37-41`, `Version.fs:10-17` | S |
| 3 | Replace `Contains`-based state sniffing with word-boundary matching; fix `(unanswered)`→answered and the `"No "` finding sentinel | `Clarification.fs:190-204`, `Checklist.fs:180-187`, `Plan.fs:215-273`, `ParsingMid.fs:494,1003` | S–M |
| 4 | Add a timeout/kill policy to `runProcess`; move the `Confirm` prompt to stderr; sense stderr redirection for blocked-report rendering | `CommandEffects.fs:131,182`, `Program.fs:234` | S |
| 5 | Delete or implement `OverwritePolicy` and the `EmitStdout`/`EmitStderr`/`SetExitCode` effects; remove the dead staleness check and the phantom `unsupportedTaskStatus` branch | `CommandTypes.fs:455-457`, `CommandEffects.fs:42-48,260-268`, `ParsingTasks.fs:715-719`, `CommandReports.fs:1087` | S |
| 6 | Emit diagnostics for silently-dropped malformed ids; align `parseWorkModel` version policy with `classifyRaw`; unify CRLF normalization between the two sha256 implementations | `Internal.fs:234-244`, `WorkModel.fs:579`, `SkillMirror.fs:12-16` / `SchemaVersion.fs:124-127` | M |
| 7 | Extract `writeReadinessEnvelope` (verify/ship/analysis), `workModelOrBlocked` (9×), the front-matter validator (10×), and the rich-console builder (3×); unify the drive loop between `Program.fs` and `ValidationRunner` | Commands/Cli/Validation | M |
| 8 | Split `CommandReports.fs` (diagnostic catalog / next-action policy / report assembly); replace the 12-tuple with a record; type an `IsDefect` bit on diagnostics instead of `providerDefectIds` | `CommandReports.fs`, `CommandWorkflow.fs` | M |
| 9 | Render `skillDriftPaths` in text/rich; refresh stale doc surfaces (README doctor/upgrade, index.md, DEVELOPING.md, unknownCommand list, reseed NextAction) | `CommandRendering.fs:246-285`, docs | S |
| 10 | Hermetic `nuget.config` (`<clear/>` + source mapping); NuGet caching in CI; fantomas + `.editorconfig` + format gate; widen the warning ratchet | `nuget.config`, workflows, props | S–M |
| 11 | Test infra: serialize/scope env mutations (Acceptance parallelization, PATH swap), delete or wire the ~106 orphaned fixture manifests, unify `FSGG_UPDATE_BASELINE`, add temp-dir cleanup (tests and `validate`) | `tests/`, `ValidationRunner.fs:105-108` | M |
| 12 | Longer-term: shared readiness-envelope schema for the three JSON views; drop `[<AutoOpen>]` flat scope in `CommandWorkflow/`; rename `Parsing{Early,Mid,Tasks}` by responsibility; DU-ify view-state/step-outcome strings; consider a drift guard tying CLAUDE.md ↔ AGENTS.md | src | L |

## 7. Verdict

The codebase's core bet — a pure MVU planner core behind a single effect
interpreter, deterministic hand-ordered serialization, contract-first
drift-guarded artifacts — is paying off and is verifiably intact. Since the
June review the team eliminated the god module and drove warnings from ~290
to 2, which demonstrates these reports get acted on. The residual risk is
concentrated in two places: **authored-input parsing** (crash classes and
keyword sniffing at the exact surface hand-edited by product authors) and
**CI enforcement** (a gate that proves compilation, not behavior). Both are
cheap to fix relative to the discipline already invested everywhere else.
