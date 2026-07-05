# Phase 0 Research: Bind SDD authoring skills to the CLI gate grammar

**Feature**: 081-skill-gate-binding · **Date**: 2026-07-05

This feature is documentation-and-diagnostics work over an existing F#/.NET codebase; there are no new external technologies. Research resolves *how* to bind two existing surfaces (authored `fs-gg-sdd-*` skills ↔ the compiled `fsgg-sdd` gates) durably, given the repo's existing test harness, skill-mirroring guards, and diagnostic layering.

## Decision 1 — Doctest anchoring: corpus-anchored, not fragment-assembly

**Decision**: Ship a single **coherent, complete, gate-passing example corpus** (one work item's full artifact set) under `docs/examples/lifecycle-artifacts/`. The doctest runs the *corpus* through the **real gate commands** via the existing `TestSupport` harness and asserts zero blocking diagnostics. Each stage skill's runnable example is bound to the corpus by a **consistency check**: the skill's marked example block must be a normalized match of (or contained in) the corresponding corpus file.

**Rationale**:
- Gates such as checklist coverage and CHK back-references are **cross-artifact** — they only run meaningfully over a coherent set (spec + clarifications + checklist), not an isolated fenced fragment. A complete corpus is the only thing a real gate can accept.
- The corpus is run through the *real* compiled gate every build, so a form the gate rejects fails CI (satisfies FR-001/002/003). The skill↔corpus consistency check guarantees the skill *teaches* that same gate-passing form (satisfies FR-006/007/008). Together they close the drift both directions.
- Reuses `TestSupport` (temp project + `runCharter/runSpecify/runClarify/...`) already proven in `tests/FS.GG.SDD.Commands.Tests`; no new harness.

**Alternatives considered**:
- *Fragment-assembly*: extract each skill fragment and splice it into a synthetic scaffold before running the gate. Rejected — fragile assembly logic, and a fragment that passes in one synthetic scaffold tells the author little about the coherent set they actually author.
- *Skill-example-is-the-only-source (no corpus)*: rejected — FR-005 mandates a copyable corpus, and fragments can't exercise cross-artifact gates.

**Scope refinement (from Phase-1 empirical baseline).** Running the real gates on the corpus revealed the CLI's generation model: `checklist`/`plan`/`tasks` are **generated views** the commands regenerate from the pure authored inputs (`charter`/`spec`/`clarifications`), and `tasks` expands a 2-FR spec into a ~9-task ladder → **14 evidence obligations**. A hand-authored 2-entry `evidence.yml` therefore can never turn the `evidence` gate green (that needs an unrelated 14-obligation ladder). So the doctest is scoped honestly:
- **charter → analyze** run on the pure authored corpus inputs (regenerating the views), asserting each stage is **not blocked**. This is green today and directly exercises the specify coverage form (#141) and the clarify `sourceSpec` front matter (#143) — the reported early-stage bugs.
- **evidence deferral (#142)** is proven by a **focused contract test** on the *standard* `TestSupport` obligation ladder (`initializeAnalyzedProject` + the passing-task ladder): swap one satisfying entry for a complete 4-field deferral → assert `deferred` disposition and **no** `missingDeferralRationale`; drop a field → assert `missingDeferralRationale`. This tests the exact deferral-field gate rule without an off-topic full-ladder corpus.
- The corpus stays a **hand-authored, readable, parser-valid** set (kept honest by `ExampleArtifactsContractTests`), with `evidence.yml` gaining an on-theme 4-field deferral as the copyable #142 example. Skill examples bind to it via the consistency check. **This scoping is stated, not silent** (no-silent-caps): the doctest does not claim to green the full evidence→ship chain on the corpus, because the CLI's generation model makes that a different, unrelated fixture.

## Decision 2 — Marking runnable examples in `SKILL.md`

**Decision**: Precede each runnable fenced block in a stage skill with a lightweight machine-readable HTML comment marker naming the corpus file it must match, e.g. `<!-- fsgg-sdd:example corpus=spec.md -->`. The extractor keys on the marker, not the language tag. Blocks with no marker (command invocations, prose snippets, explicit counter-examples) are ignored; a counter-example may carry `<!-- fsgg-sdd:example counter -->` to be asserted *not* to pass.

**Rationale**: Skills contain many fenced blocks (`text` command lines, `markdown`, `yaml`) — only some are artifact examples (§skill-fence audit: 2–8 fences per skill). Inferring by language tag is ambiguous and brittle. A comment marker keeps `SKILL.md` fully hand-authored and human-readable (Principle VII: skills are not a second source of truth — the marker adds no *content*, only a binding annotation), survives the byte-identical `.claude`↔`.codex` mirror, and is trivially greppable.

**Alternatives considered**: language-tag heuristic (rejected: false positives on command fences); a separate sidecar manifest listing example line-ranges (rejected: drifts from the skill it describes — the very failure mode this feature fixes).

## Decision 3 — FR-009 field-list binding: one shared canonical list, checked

**Decision**: Introduce a small **enumerable required-keys registry in the typed layer** — `requiredFrontMatterKeys : LifecycleStage -> string list` and `requiredDeferralKeys : string list` — as the single authoritative source, then bind three surfaces to it:
1. the relevant **parsers/handlers** assert their required tuple against the registry (behavioral parser tests already prove a missing required field blocks — e.g. `Clarification.fs:121-122` requires `schemaVersion`/`workId`/`stage`/`sourceSpec`);
2. the **`fs-gg-sdd-authoring-contracts` §5 table** (the one consolidated human enumeration, `SKILL.md` lines 113-119) is checked to match the registry;
3. each stage skill's marked field-list region is checked to match the registry for its stage.

The **check** (a test) asserts set-equality between each surface and the registry; the build fails on any asymmetric difference, naming the field and the surface. Skills stay hand-authored (per clarification — no codegen/generated regions).

**Rationale**: The agent survey confirmed required-field sets are **scattered per parser** (each `parseXFrontMatter` has its own tuple match) — there is *no* reflectable code-level registry today, and the only consolidated enumeration is documentation (the authoring-contracts §5 table). "Checked against the typed contract" (FR-009) is only meaningful if we first make the contract a *single value*; a minimal `LifecycleStage -> string list` registry is that value, and behavioral parser tests keep it honest against the actual gate. Check-over-codegen was the user's clarified choice: lowest new machinery, consistent with the doctest, keeps `SKILL.md` free of generated regions that would fight the mirror/seed guards.

**Scope note**: fully refactoring every parser to *consume* the registry (rather than assert against it) is a larger internal change; the plan treats the registry + behavioral assertions as required, and the parser-consumes-registry refactor as an optional follow-on that does not change gate behavior.

**Alternatives considered**: reflection over `EvidenceDeclaration` (rejected — its fields are `string option`; "required for a deferral" is a gate rule, not a type-level fact, so reflection over/under-reports). Checking only the §5 table and skipping a code-level registry (rejected — leaves the "typed contract" half of FR-009 as prose-vs-prose, not prose-vs-contract).

## Decision 4 — Diagnostic split (FR-010): fix at the source, not the remap

**Decision**: The missing-`[CHK:CHK-###]` case at `Checklist.fs:244` (function `checklistReferenceDiagnostics`) currently emits the Artifacts-layer `Diagnostics.workModelInconsistent`; the Commands remap `ChecklistPlanAuthoring.fs:79` (`| "workModelInconsistent", _ -> malformedChecklistFrontMatter …`) then buckets it under front matter. Fix across the two layers:
- **Artifacts layer**: emit a dedicated diagnostic id (`missingChecklistBackReference`) at the `Checklist.fs` source site instead of reusing `workModelInconsistent`.
- **Commands layer**: add a `missingChecklistBackReference` constructor and a remap case so the new id surfaces as itself; the `workModelInconsistent` remap now catches only genuine front-matter/schema mismatches. The other front-matter/schema causes (`malformedSchemaVersion`, `unsupportedSchemaVersion`, `futureSchemaVersion`) keep `malformedChecklistFrontMatter`.

Every dependent surface moves in lockstep (agent-confirmed edit set): `DiagnosticConstructors.fs` (constructor + the stage-classifier lists at ~1033/1068), `CommandReports.fs`/`.fsi` (re-export + `val`), `RemediationPointers.fs` (a back-reference pointer, not a front-matter one), `ViewGeneration.fs` (category classifier ~297), and the baselines/tests (`PublicSurface.baseline` in Commands *and* Cli, `RemediationPointersTests.fs`, `SurfaceBaselineTests`).

**Rationale**: The misnaming originates because a *content* diagnostic (missing back-ref) shares the `workModelInconsistent` code the Commands layer treats as "front matter". A distinct id emitted at the source is precise and leaves the true front-matter cases untouched. No deprecated alias (clarified: SDD-internal, not a consumed cross-repo contract — confirmed: zero references in `.github`/Governance; only SDD `PublicSurface.baseline` rows). `RemediationPointersTests` asserts every diagnostic id has a remediation pointer, so the new id needs one to keep that guard green.

**Alternatives considered**: only re-message the shared diagnostic (rejected in clarify — would misname the schema-version cases); keep `workModelInconsistent` but special-case the remap by message text (rejected — brittle string matching).

## Decision 5 — Skill edits propagate through the mirror + seed guards

**Decision**: All skill-body edits are made to the canonical `.claude/skills/fs-gg-sdd-*/SKILL.md` and mirrored **byte-identically** to `.codex/skills/fs-gg-sdd-*/SKILL.md` in the same change. The `.claude` bodies are embedded as `SeededSkill.*` resources (`.fsproj`) and seeded to all three roots by init/scaffold; `.agents` copies are seed-time only (not committed here).

**Rationale**: `tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs` guards `.claude`≡`.codex`; `SeededSkillsTests.fs` / `ProcessSkillManifestTests.fs` guard the seeded set and the `skill-manifest` (canonical-body `sha256`). Any skill edit that skips the mirror or the manifest regen fails these guards. Editing a skill therefore also requires regenerating the process `skill-manifest` (`fsgg-sdd registry skill-manifest --write`). This is captured as explicit tasks, not left implicit.

## Decision 6 — `.fsgg/early-stage-guidance.md` currency

**Decision**: Verify (and update if stale) `.fsgg/early-stage-guidance.md`, the read-only mirror of the live early-stage (charter/specify/clarify/checklist) contract, since this feature touches the specify FR form, the clarify `sourceSpec` field, and the checklist back-ref diagnostic — all early-stage surfaces it mirrors. Its drift-guard test is the arbiter of whether an update is required.

**Rationale**: CLAUDE.md pins `early-stage-guidance.md` as a drift-guarded mirror of the same contracts the skills teach; a fix to the authoring grammar that leaves the mirror stale would trip (or should trip) that guard. Confirming currency keeps the two authored mirrors coherent.

## Resolved unknowns / confirmed facts

- **Harness**: `TestSupport` in `FS.GG.SDD.Commands.Tests` builds a temp workspace and runs stage commands in-process; the doctest reuses it. No network, offline (FR-013).
- **Change tier**: **Tier 1** — touches the agent-skill contract, a command-output diagnostic identifier (JSON contract), and public surface (`PublicSurface.baseline`). Requires spec, plan, tasks, `.fsi` updates where code changes, tests, docs, and migration notes.
- **Schema/migration posture**: No persisted-schema version bump. The JSON diagnostic contract change is **additive** (new `missingChecklistBackReference` identifier; the back-ref case's emitted identifier moves). `scaffold-provenance` and all `v1` schemas are untouched.
- **Current corpus state**: `docs/examples/lifecycle-artifacts/{charter,spec,clarifications,checklist,plan,tasks,evidence}` exist. `evidence.yml` has only `result: pass` entries (no deferral) — must gain a deferral-bearing example. The corpus's `spec.md`/`checklist.md` must be confirmed/repaired to a coherent gate-passing set (the corpus is the doctest's input of record).
- **Existing coverage gap**: `tests/FS.GG.SDD.Artifacts.Tests/ExampleArtifactsContractTests.fs` runs the corpus through the **artifact parsers**, not the **`fsgg-sdd` gate commands**, and nothing runs the *skill* fenced blocks anywhere. The new doctest (gate-command level) and the skill↔corpus consistency check fill exactly that gap — they complement, not duplicate, the parser-level test.
- **Skill-manifest**: editing any `SKILL.md` body changes its canonical `sha256`, so `fsgg-sdd registry skill-manifest --write` must regenerate `.agents/skills/skill-manifest.json`, and `ProcessSkillManifestTests`/`SkillMirrorTests` must stay green.
