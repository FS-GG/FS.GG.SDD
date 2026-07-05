# Phase 0 Research: Pre-flight authoring lint

All Technical-Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains.

## D1 — Command surface: real `SddCommand` vs peer verb

**Decision**: Implement `lint` as a real `SddCommand` case (not a `validate`-style peer verb
dispatched before `parseCommand`).

**Rationale**: A real `SddCommand` flows through the existing MVU run loop
(`CommandEffects.driveToReport`) and the `CommandReport` → json/text/rich projection pipeline for
free (FR-010), so the only bespoke code is the exit branch. `<stage> --explain` must reuse the
stage handlers anyway, so the engine already lives in the command core.

**Alternatives considered**: a peer verb like `validate`/`registry` (its own arg parse + bespoke
returns). Rejected — it would re-implement the three-projection rendering `lint` gets for free as
an `SddCommand`.

## D2 — Reuse the live parsers; do not re-derive grammars

**Decision**: The lint engine routes an artifact to the **existing** `FS.GG.SDD.Artifacts` stage
parsers and surfaces the `Diagnostic list` they already return. It adds no new grammar.

**Rationale**: Constitution II (the parser is the machine contract) and IV (simplicity). The four
classes are already detected by shipped code:

| Class | Reuse target (file) | Emitted id |
|---|---|---|
| Coverage-line shape ("counted but uncovered") | `Specification.requirementReferences` / `missingIdDiagnostics` (`LifecycleArtifacts/Specification.fs`), `Checklist.parseChecklistFacts`, `ChecklistPlanAuthoring.requirementCoverage` | `failedRequirementsQuality` |
| Missing `[AMB:AMB-###]` tag | `Clarification.parseClarificationFacts` blocking-ambiguity resolution (`LifecycleArtifacts/Clarification.fs`) | `missingClarificationAnswer`, `unresolvedBlockingAmbiguity` |
| Front-matter incompleteness | per-stage gating-field check inside each `parse*Facts` / `WorkItemMetadata.parseWorkItemMetadata` | `malformed{Charter,Specification,Clarification,Checklist,Plan}FrontMatter` |
| Duplicate ids | `Internal.duplicateScopedDiagnostics` remapped per stage | `duplicate{WorkId,Specification,Clarification,Checklist,Plan,Task,Evidence}Id` |

The `Diagnostic` type already carries `Location { Line; Column }` and a `Correction` fix-hint slot,
so FR-007's fix hint is present without new work.

**Alternatives considered**: a standalone lint grammar/validator. Rejected — it would drift from
the stage's real enforcement, reintroducing exactly the silent-mismatch problem the feature fixes.

## D3 — Which diagnostics does lint surface? (scope vs the "four classes")

**Decision**: Lint surfaces **all Error-severity diagnostics** the routed parser returns for the
artifact. The four named classes are guaranteed covered because each is an `Error`. Non-defects
(optional `sha256:` Source-Snapshot digests, empty-section disclaimers, etc.) are never emitted as
Errors and therefore never appear (satisfies FR-017 "all findings are errors" and the edge case
"optional digests are not defects").

**Rationale**: Filtering to an exact id allow-list would drift as new Error diagnostics are added
and is more code; surfacing the parser's Errors is simpler, stays clean on the canonical examples
(they already return zero blocking diagnostics — `ExampleArtifactsContractTests`), and is honest
("lint = what the stage parser would block on").

## D4 — Coverage line is a single-artifact **shape** check, not cross-artifact reconciliation

**Decision**: For FR-003, lint detects the coverage-line **grammar/shape** defect (a bullet that
looks like a coverage line but does not match the strict `- FR-###: … (covers AC-###)` form, so it
silently fails to bind) from the **checklist artifact alone**. It does **not** perform cross-artifact
FR→AC coverage reconciliation against the spec — that stays the job of the `checklist` stage and
`analyze`.

**Rationale**: The "counted but uncovered" silent trap is exactly the shape mismatch, detectable
single-artifact via the strict-regex-vs-loose-bullet comparison (`missingIdDiagnostics`,
`IsMatch bullet && not IsMatch pattern`). Keeping lint single-artifact honors the spec's
single-artifact assumption and avoids lint quietly becoming a second `analyze`.

## D5 — Artifact-kind auto-detection (FR-002)

**Decision**: Detect kind in this order: (1) parse the Markdown front matter and read the closed
`stage:` vocabulary (`Core.frontMatter`); (2) if absent, fall back to filename/extension
(`evidence.yml` → evidence, `tasks.yml`/`*.tasks.yml` → tasks, `clarifications.md`/`checklist.md`
by name). If neither yields a recognized kind → **unusable input** (exit 2), reported as a single
"cannot determine artifact kind" defect (not a crash).

**Rationale**: `stage:` is the authoritative kind signal the stages themselves key on; filename is
the pragmatic fallback for the YAML artifacts that carry no stage front matter. Deterministic and
dependency-free.

## D6 — Exit-code polarity (FR-011): bespoke `exitCodeForLint`

**Decision**: `lint` (and `<stage> --explain`) use a dedicated mapping — **0** clean, **1** defects
in a well-formed artifact, **2** unusable input (missing/unreadable/unrecognized). This is applied
by a `command = Lint || request.Explain` branch in `Program.fs`; every other command keeps the
shared `exitCodeForReport`.

**Rationale**: `ReportAssembly.exitCodeForReport` reserves **2** for the tool-defect class
(`IsToolDefect`) and **1** for malformed user input — the opposite polarity. Bad user input is not
a tool defect, so we must not set `IsToolDefect` to reach 2. Peer/cross-cutting verbs already carry
bespoke exit logic (e.g. `printValidate` in `Program.fs`), so a lint-specific mapping is idiomatic,
not a new pattern. This is the one documented divergence (plan Complexity Tracking).

**`--explain` exit**: `--explain` shares the same 0/1/2 mapping. "Non-blocking" (FR-016) means it
does not mutate or advance the stage — **not** that it always exits 0. This keeps `--explain`
usable as a gate identically to `lint`.

## D7 — FR-007 grammar-pointer map (new, drift-guarded)

**Decision**: A pure table maps each defect class (or diagnostic id) → a stable anchor in
`docs/reference/authoring-contracts.md` (headings already exist: `## Acceptance coverage line`,
`## Clarify decision-tag resolution`, `## Per-stage front matter`) plus, where useful, the tagged
example fence (`coverage:accepted`, `clarify-decision:resolved`, `front-matter:*`). A drift-guard
test (`LintGrammarPointerTests`, mirroring `AuthoringDocsContractTests` block extraction) asserts
every pointer resolves to a real heading/tag.

**Rationale**: No id→anchor catalog exists today; this is the one genuinely new contract. Anchoring
to the feature-046 grammar-of-record doc (and drift-guarding it) keeps the pointer honest and
reuses the existing tagged-fence convention.

## D8 — Read-only MVU shape (Constitution V)

**Decision**: `HandlersLint` mirrors `HandlersDoctor` — plan a single `ReadFile <artifact>` effect;
run `LintEngine` on the returned snapshot; assemble the report; emit **no** mutating effect. No new
edge interpreter (`CommandEffects.interpret` already reads files). `--explain` reuses the stage
handler with mutating effects gated on `not Explain`.

**Rationale**: `doctor` is the proven read-only precedent (no write effect, computes a projection).
Reusing the edge keeps I/O at the one interpreter boundary.

## D9 — Determinism (FR-012/SC-005)

**Decision**: Defects are ordered by `(Location.Line, Location.Column, Id)` with a stable tie-break;
the report is serialized through the existing deterministic `CommandSerialization`. No timestamps or
environment-derived values enter the report.

**Rationale**: The parsers are pure over file bytes; the only ordering freedom is the defect list,
which we fix explicitly. Golden json tests lock it.

## Corrections to the original feedback framing

- The `[AMB:...]` **bracket** is a human convention; resolution is by a bare `AMB-###` token on a
  `DEC-###` line (per `authoring-contracts.md:219` and feature 075). Lint reports the *unresolved
  blocking ambiguity*, phrased around the decision-tag mechanism, not a literal bracket-presence
  regex.
- `sha256:` Source-Snapshot digests are **optional** and are not a defect (feature 075) — lint must
  not report them (D3 guarantees this since they are not Error diagnostics).
