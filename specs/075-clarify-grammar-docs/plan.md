# Implementation Plan: Document the clarify decision-tag grammar and per-stage front-matter

**Branch**: `075-clarify-grammar-docs` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/075-clarify-grammar-docs/spec.md`

## Summary

TD1 field feedback (#122) showed the `clarify` stage blocking four times because its
load-bearing grammars live only in a shipped example, not in the skills or reference doc.
This feature documents those grammars accurately and makes them **drift-guarded**:

1. In the `fs-gg-sdd-clarify` skill body — the decision-tag resolution mechanism (AMB id on a
   `DEC-###` line under `## Decisions`/`## Accepted Deferrals`, plus the `## Remaining
   Ambiguity` interaction), the answer-vs-tag distinction, and the `duplicateClarificationId`
   declaration-vs-reference trap.
2. In the `fs-gg-sdd-authoring-contracts` skill body — the per-stage **gating** front-matter
   field sets (distinct from template-defaulted fields), the closed `stage` vocabulary, the
   free-string nature of `changeTier`/`status`, and the truth about `Source Snapshot`/`sha256`
   (a checklist/plan optional concept, not a clarify requirement).
3. In `docs/reference/authoring-contracts.md` — new **labelled, parser-validated** example
   blocks for the decision-tag grammar and front-matter, with `AuthoringDocsContractTests`
   extended to run them through the live parsers, so the newly documented grammar cannot drift
   from the tool.

Approach: the live parser is the source of truth (Constitution II). The skill bodies carry
human-facing guidance and point to the drift-guarded reference for the authoritative form. The
change is documentation + a drift guard; it does not alter parsing, gating, schemas, or the CLI
surface. Research corrected three inaccurate premises in the original feedback (front-matter
required sets, the `[AMB:...]` bracket being a convention not a contract, and sha256 not being a
clarify concept); the spec FRs were updated accordingly.

## Technical Context

**Language/Version**: F# on .NET `net10.0` (docs + one test-file extension; no product `.fs`/`.fsi` behavior change).

**Primary Dependencies**: Existing SDD parsers (`FS.GG.SDD.Artifacts` `Clarification`/`Checklist`/`Plan`/`Specification`/`Charter` front-matter parsers), the seeded-skill embedding/mirror (`SeededSkills.fs`, `FS.GG.Contracts` `SkillMirror`), the skill-manifest generator (`ProcessSkillManifest`, `fsgg-sdd registry skill-manifest`).

**Storage**: Files only — Markdown skill bodies (`.claude`/`.codex`), the reference doc, and the regenerated `.agents/skills/skill-manifest.json`.

**Testing**: xUnit test projects — `SeededSkillsTests`, `ProcessSkillManifestTests`, `AuthoringDocsContractTests` (extended here), `ExampleArtifactsContractTests`, `SkillMirrorTests`.

**Target Platform**: Cross-platform CLI/library (Linux CI).

**Project Type**: Single project (F# lifecycle CLI + libraries) — documentation of an existing agent-skill contract.

**Performance Goals**: N/A (documentation).

**Constraints**: Seeded skills must stay byte-identical across `.claude`/`.codex` (and, downstream, `.agents`); the skill-manifest sha256 must be regenerated; all new reference-doc example blocks must pass through the live parser. No change to what the parser requires.

**Scale/Scope**: 2 skill bodies × 2 committed roots (4 SKILL.md files) + 1 reference doc + 1 test file extension + 1 regenerated manifest. No new `.fsi` surface.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — No public F# surface added; the only
  code change is extending `AuthoringDocsContractTests.fs` (a test) to validate new example
  labels. No `.fsi` change. **PASS** (spec authored; the "semantic test" is the extended
  contract test that fails if the documented grammar diverges from the parser).
- **II. Structured Artifacts Are the Machine Contract** — The parser is authoritative; the docs
  are made to match it and are pinned by a live-parser test. Prose-vs-structure conflict is
  resolved in favour of the parser, and the reference-doc example blocks are the drift-guarded
  record. **PASS**.
- **III. Visibility in `.fsi`** — No public module surface change. **PASS (N/A)**.
- **IV. Idiomatic Simplicity** — Test extension reuses the existing label-dispatch pattern in
  `AuthoringDocsContractTests`. **PASS**.
- **V. Elmish/MVU boundary** — No stateful/I/O workflow added. **PASS (N/A)**.
- **VI. Test Evidence Is Mandatory** — The new grammar examples become failing-if-wrong
  evidence via the extended contract test; the skill-manifest regeneration is covered by
  `ProcessSkillManifestTests`. **PASS**.
- **VII. Agent & Human Workflows Share One Contract** — Editing both `.claude` and `.codex`
  byte-identically and regenerating the manifest keeps Claude, Codex, and downstream `.agents`
  on one contract. **PASS** (drift guards enforce it).
- **VIII. Observability & Safe Failure** — No diagnostic behavior change; the docs explain
  existing diagnostics (`malformed*FrontMatter`, `missingClarificationAnswer`,
  `unresolvedBlockingAmbiguity`, `duplicateClarificationId`) accurately. **PASS**.

**Change tier: Tier 1** — the seeded skill bodies are a drift-guarded agent-skill contract and
their sha256 manifest is a structured artifact; a body change regenerates the manifest. Requires
spec, plan, tasks, tests (the extended contract test + manifest test), and docs. No `.fsi`/schema
migration. No Complexity Tracking entries required — no gate violations.

## Project Structure

### Documentation (this feature)

```text
specs/075-clarify-grammar-docs/
├── plan.md              # This file
├── spec.md              # Feature spec (FR-001..008, SC-001..004)
├── research.md          # Phase 0 — parser truth + edit path + corrections
├── data-model.md        # Phase 1 — the documented grammar entities
├── quickstart.md        # Phase 1 — how to validate the docs end-to-end
├── contracts/
│   └── documented-grammars.md   # The authoritative grammar the docs must match + new test labels
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root — files this feature touches)

```text
.claude/skills/fs-gg-sdd-clarify/SKILL.md                 # canonical body (embedded resource)
.claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md     # canonical body (embedded resource)
.codex/skills/fs-gg-sdd-clarify/SKILL.md                  # byte-identical mirror (hand-maintained)
.codex/skills/fs-gg-sdd-authoring-contracts/SKILL.md      # byte-identical mirror (hand-maintained)
.agents/skills/skill-manifest.json                         # regenerated (per-skill sha256)
docs/reference/authoring-contracts.md                      # + decision-tag & front-matter grammar sections (labelled fences)
tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs  # + handlers for the new example labels
```

Unchanged and relied upon: `src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs`,
`FS.GG.SDD.Commands.fsproj` (embedded-resource links), `src/FS.GG.SDD.Cli/RegistrySkillManifest.fs`,
`docs/examples/lifecycle-artifacts/clarifications.md` (already parser-validated exemplar).

**Structure Decision**: Single-project layout. This feature edits committed Markdown skill
bodies (two roots), one reference doc, one regenerated manifest, and extends one existing test
file — no new source modules or `.fsi` files.

## Ordered implementation approach (feeds /speckit-tasks)

1. **Reference doc first (drift-guarded source).** Add to `docs/reference/authoring-contracts.md`:
   (a) a "Clarify decision-tag resolution" section with labelled example blocks the contract test
   parses (resolving decision, accepted-deferral, the answers-don't-resolve case, the duplicate
   trap), and (b) a "Per-stage front matter" section with a gating-vs-defaulted table and
   labelled front-matter example blocks. Choose fence labels consistent with existing ones
   (e.g. `clarify-decision:resolved`, `clarify-decision:deferred`, `front-matter:clarify`, …).
2. **Extend `AuthoringDocsContractTests.fs`** with handlers for the new labels, running each
   block through the live parser (`Clarification.parseClarificationFacts` and the stage
   front-matter parsers) and asserting the intended accept/reject outcome. This is the failing
   test that proves the documented grammar matches the tool.
3. **Author the `fs-gg-sdd-clarify` skill body** (`.claude` canonical) — decision-tag mechanism,
   answer-vs-tag, duplicate trap, and a worked example mirroring the shipped exemplar; point to
   the drift-guarded reference for the authoritative grammar.
4. **Author the `fs-gg-sdd-authoring-contracts` skill body** (`.claude` canonical) — add the
   per-stage front-matter contract (gating vs defaulted, `stage` vocabulary, free-string
   `changeTier`/`status`) and the `Source Snapshot`/`sha256` correction; update the "three
   grammars" framing to include the newly documented ones without misstating the count.
5. **Mirror both bodies byte-identically into `.codex/skills/…`.**
6. **Rebuild** `src/FS.GG.SDD.Commands` so the embedded resources refresh.
7. **Regenerate the manifest**: `dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write`; confirm `--check` exits 0.
8. **Run guard tests** green (SeededSkillsTests, ProcessSkillManifestTests, AuthoringDocsContractTests, ExampleArtifactsContractTests, SkillMirrorTests) and a full `dotnet test`.
9. **Validate SC-001** with the quickstart: a reader following only the two skill bodies can author a `clarifications.md` the live `clarify` accepts.

## Complexity Tracking

No Constitution gate violations — table intentionally omitted.
