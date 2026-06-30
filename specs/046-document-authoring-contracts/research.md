# Phase 0 Research: Authoring Contracts & Diagnostics

All findings re-verified against `main` during planning (the issue was filed against
`fsgg-sdd` 0.2.1; line numbers below are current and the plan re-verifies them before edits).

## R1 — The coverage (strict-scan) grammar

- **Decision**: Document the accepted form as a list item `- FR-###:` with the acceptance
  reference on the **same line**; document the rejected forms (bold `**FR-###**`, bracketed-only
  AC tags, colon-less lines). Do not change the grammar.
- **Verified source**: `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Specification.fs` —
  `requirementReferences` matches `^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$` (case-insensitive), then
  scans the **same line** for `AC-\d{3,}` and `US-\d{3,}`. A separate loose scan
  (`\bFR-\d{3,}\b`) feeds the CHK item list, which is why a bold-id requirement is *counted*
  but *uncovered*.
- **Coverage consumer**: `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs`
  `requirementCoverage` collects `AcceptanceScenarioIds` from those references; `hasCoverage`
  drives the CHK review `pass`/`fail` (the `fail`-branch `Correction` is at `ParsingMid.fs:163`).
- **Rationale**: Documentation + a self-correcting diagnostic fully removes the "forced
  decompilation" defect without the larger blast radius of relaxing the grammar (golden tests,
  Governance handoff). Grammar relaxation is issue fix 4 — explicitly out of scope.
- **Alternatives considered**: Relax the parser to accept bold/bracketed forms (rejected:
  out of scope per requester); accept `(covers AC-###)` anywhere (rejected: same).

## R2 — The `evidence.yml` vocabulary and satisfaction rule

- **Decision**: Document the full `kind` vocabulary, the meaningful `result` values, the
  satisfaction rule, and the `synthetic`/`deferral`/unknown-kind behaviors.
- **Verified source**:
  - `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` — `EvidenceKind` =
    `Implementation | Verification | Review | GeneratedViewEvidence | Synthetic | Deferral |
    Note | Missing`; `parseEvidenceKind` accepts `implementation | verification | review |
    generated-view | generatedview | synthetic | deferral | note | missing` and **falls back
    to `Verification`** for anything else (silent — must be documented, FR-004).
  - `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs` — disposition ladder: an
    obligation is **satisfied** iff a matching declaration has
    `normalizedEvidenceResult result = "pass"` **and** `synthetic = false`; a synthetic `pass`
    yields disposition `synthetic` (not satisfied); `deferred`/`Deferral` → `deferred`;
    `stale`/`advisory` recognized. `normalizedEvidenceResult` (in `HandlersEvidence.fs`) is
    `trim+lowercase`.
  - Recognized result vocabulary (from `HandlersEvidence.fs`):
    `pass | fail | deferred | missing | stale | advisory | blocked`.
- **Rationale**: These are the exact rules an author must satisfy to clear `verify`; today they
  exist only in code. Documenting the silent `verification` fallback prevents the "my kind
  worked but isn't what I wrote" surprise.
- **Alternatives considered**: Hard-fail on unknown `kind` instead of documenting the fallback
  (rejected: behavior change beyond docs+diagnostics scope; FR-012).

## R3 — `missingSpecificationIntent` already names the missing facts

- **Decision**: FR-009 reduces to (a) enriching the **Correction** text to show the labeled
  intent form and (b) documentation — the **Message** already names the specific missing facts.
- **Verified source**:
  - `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingEarly.fs` (~line 168) computes
    `missing = [ if UserValue None -> "user value"; if Scope empty -> "scope"; if Requirements
    empty -> "measurable requirement" ]` from the **parsed** input, not a static list.
  - `src/FS.GG.SDD.Commands/CommandReports.fs` `missingSpecificationIntent` (~line 151) already
    interpolates `missing required facts: {missingText}` and puts `missingFacts` in `RelatedIds`.
  - The intent parser reads `label: value` lines (`value:`/`scope:`/`requirement:`…) or treats a
    bare line as `value`; free prose with no labels yields only `UserValue`, so `scope` and
    `measurable requirement` are reported missing — accurate, but the **Correction**
    ("Provide input with value, scope, and requirement facts…") never shows the labeled form.
- **Rationale**: The issue's §1.4 complaint ("doesn't say which fact") is largely already met by
  the Message; the residual gap is the opaque expected **format**. Enriching Correction to show
  the labeled form (e.g. `value: … / scope: … / requirement: …`) plus documenting it closes it.
- **Alternatives considered**: Rework the intent parser to extract facts from arbitrary prose
  (rejected: behavior change, out of scope; risks regressing existing specify goldens).

## R4 — `correction`/`message` are serialized JSON fields → Tier 1

- **Decision**: Enrich the single shared `Correction`/`Message` string (one source of truth);
  treat as **Tier 1** and update golden fixtures. Reword FR-010/SC-004 from "byte-identical
  JSON" to "stable structure/codes/outcomes; intended string-value change."
- **Verified source**: `src/FS.GG.SDD.Artifacts/Json/JsonWriters.fs:61–70` writes `id`,
  `severity`, `message`, `correction` for every diagnostic; `ViewGeneration.fs:176,342` writes
  `correction` into `work-model.json` and the checklist/readiness views; CHK reviews carry their
  own `Text`/`Correction` into `checklist.md`.
- **Rationale**: The alternative (renderer-only enrichment to keep JSON byte-identical) splits
  the authoring guidance away from the contract and never reaches `checklist.md` or JSON
  consumers — contrary to Principles II and VII. Confirmed with the requester.
- **Impact**: Golden fixtures in `ChecklistCommandTests`, `EvidenceCommandTests`,
  `SpecifyCommandTests`, and any `VerifyCommandTests`/`CommandReportJsonTests`/work-model goldens
  that embed the three corrections regenerate. No JSON field is added/removed/renamed; no schema
  version bump.

## R5 — Documentation home and drift guard

- **Decision**: New `docs/reference/authoring-contracts.md` (linked from `docs/index.md`),
  mirrored at the `checklist`/`evidence` steps of `docs/quickstart.md`. A new test
  `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` reads the reference doc,
  extracts tagged example blocks, and asserts the parsers agree (SC-005).
- **Verified context**: `docs/index.md` already curates a docs index and a `reference/` area;
  `docs/quickstart.md` has a per-stage lifecycle table (lines ~82–95) where the coverage line
  and a satisfying `evidence.yml` snippet belong. Tests are xUnit and can read repo files via
  a stable relative path (mirror existing fixture-reading tests).
- **Drift-guard convention**: the reference doc tags canonical examples with fenced labels
  (e.g. ```text coverage:accepted / coverage:rejected / evidence:satisfied /
  evidence:unsatisfied```); the test parses each block through the **public** parse surface —
  `Specification.parseSpecificationFacts` (reading the resulting `.RequirementReferences`,
  populated by the real `requirementReferences`) and `Evidence.parseEvidence` (real
  `kind`/`result`/`synthetic` vocabulary via the real `parseEvidenceKind`) — then applies the
  non-synthetic-`pass` rule, asserting accepted⇒covered/satisfied and rejected⇒uncovered/unsatisfied.
  The raw `requirementReferences`/`parseEvidenceKind` functions are restricted by their `.fsi`
  (no `InternalsVisibleTo`), so the guard reaches them only through the public entry points — no
  public-surface change (Principle III stays PASS). The satisfaction rule is not a public
  predicate; the test re-expresses the one-line rule and is kept in sync with the handler ladder
  via T002. This makes the docs co-verified, not aspirational.
- **Alternatives considered**: A hand-maintained example list duplicated in test + doc (rejected:
  drifts — the whole defect being fixed); Markdown-prose-only docs with no test (rejected: fails
  SC-005).

## R6 — Evidence name disambiguation (FR-011)

- **Decision**: Name SDD's concept consistently as the lifecycle **`evidence.yml`** contract in
  the reference and quickstart, with a short note that a scaffolded product may ship a separate,
  unrelated "evidence" document that does not describe this contract. Stay entirely within
  SDD-owned docs/diagnostics; reference no external provider package, path, or URL.
- **Rationale**: SDD does not own or edit the external provider's product docs (Engineering
  Constraints; FR-012). The remedy SDD controls is naming its own concept unambiguously.
- **Alternatives considered**: Edit/parameterize the external provider's doc (rejected: not
  SDD-owned, and would embed provider identity in SDD).
